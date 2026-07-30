using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;
using MoviesAPI.Services;

namespace MoviesAPI.Controllers;

public class LetterboxdController : ControllerBase
{
    private const long MaxCsvFileSize = 10 * 1024 * 1024;
    private const long MaxCsvRequestSize = 11 * 1024 * 1024;

    private readonly AppDbContext _context;
    private readonly LetterboxdService _letterboxdService;

    public LetterboxdController(AppDbContext context, LetterboxdService letterboxdService)
    {
        _context = context;
        _letterboxdService = letterboxdService;
    }

    [HttpPut("user/{userId}/letterboxd/connect")]
    [Authorize]
    public async Task<IActionResult> Connect(
        int userId,
        [FromBody] ConnectLetterboxdRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsCurrentUser(userId))
        {
            return Forbid();
        }

        var username = request.Username?.Trim();
        if (string.IsNullOrEmpty(username) || !LetterboxdService.IsValidUsername(username))
        {
            return BadRequest(new { message = "Username do Letterboxd inválido." });
        }

        var user = await _context.Auth.FindAsync([userId], cancellationToken);
        if (user == null)
        {
            return NotFound(new { message = "Usuário não encontrado." });
        }

        // SyncUserAsync persiste o username junto com o resultado do RSS. Em caso de falha upstream,
        // nenhuma alteração é salva e o cliente não fica em um estado de conexão ambíguo.
        user.LetterboxdUsername = username;

        try
        {
            var sync = await _letterboxdService.SyncUserAsync(userId, cancellationToken);
            return Ok(new
            {
                data = new
                {
                    username = user.LetterboxdUsername,
                    lastSync = user.LetterboxdLastSync,
                    imported = sync.Imported,
                    updated = sync.Updated
                },
                message = "Letterboxd conectado; atividade recente sincronizada via RSS."
            });
        }
        catch (LetterboxdSyncException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    [HttpPost("user/{userId}/letterboxd/sync")]
    [Authorize]
    public async Task<IActionResult> Sync(int userId, CancellationToken cancellationToken)
    {
        if (!IsCurrentUser(userId))
        {
            return Forbid();
        }

        var user = await _context.Auth.FindAsync([userId], cancellationToken);
        if (user == null)
        {
            return NotFound(new { message = "Usuário não encontrado." });
        }

        if (string.IsNullOrEmpty(user.LetterboxdUsername))
        {
            return BadRequest(new { message = "Nenhum username do Letterboxd está conectado." });
        }

        try
        {
            var sync = await _letterboxdService.SyncUserAsync(userId, cancellationToken);
            return Ok(new
            {
                data = new
                {
                    imported = sync.Imported,
                    updated = sync.Updated,
                    lastSync = user.LetterboxdLastSync
                },
                message = "Atividade recente sincronizada."
            });
        }
        catch (LetterboxdSyncException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    [HttpPost("user/{userId}/letterboxd/import-csv")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxCsvRequestSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxCsvRequestSize)]
    public async Task<IActionResult> ImportCsv(
        int userId,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (!IsCurrentUser(userId))
        {
            return Forbid();
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Selecione um ratings.csv não vazio." });
        }

        if (file.Length > MaxCsvFileSize)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { message = "O arquivo CSV deve ter no máximo 10 MB." });
        }

        if (!await _context.Auth.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return NotFound(new { message = "Usuário não encontrado." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _letterboxdService.ImportRatingsCsvAsync(userId, stream, cancellationToken);

            return Ok(new
            {
                data = result,
                message = "Histórico completo de avaliações do Letterboxd importado."
            });
        }
        catch (LetterboxdCsvValidationException ex)
        {
            return UnprocessableEntity(new
            {
                message = ex.Message,
                errors = ex.Errors
            });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Outra importação alterou o histórico ao mesmo tempo. Tente novamente." });
        }
    }

    [HttpDelete("user/{userId}/letterboxd/disconnect")]
    [Authorize]
    public async Task<IActionResult> Disconnect(int userId, CancellationToken cancellationToken)
    {
        if (!IsCurrentUser(userId))
        {
            return Forbid();
        }

        var user = await _context.Auth.FindAsync([userId], cancellationToken);
        if (user == null)
        {
            return NotFound(new { message = "Usuário não encontrado." });
        }

        user.LetterboxdUsername = null;
        user.LetterboxdLastSync = null;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Letterboxd desconectado. O histórico importado foi preservado." });
    }

    [HttpGet("user/{userId}/letterboxd/status")]
    [Authorize]
    public async Task<IActionResult> Status(int userId, CancellationToken cancellationToken)
    {
        if (!IsCurrentUser(userId))
        {
            return Forbid();
        }

        var user = await _context.Auth.FindAsync([userId], cancellationToken);
        if (user == null)
        {
            return NotFound(new { message = "Usuário não encontrado." });
        }

        var counts = await _letterboxdService.GetStatusCountsAsync(userId, cancellationToken);

        return Ok(new
        {
            data = new
            {
                username = user.LetterboxdUsername,
                lastSync = user.LetterboxdLastSync,
                lastImport = user.LetterboxdLastImport,
                letterboxdMovies = counts.LetterboxdMovies,
                totalMovies = counts.TotalMovies
            }
        });
    }

    [HttpPost("user/{userId}/letterboxd/backfill-tmdb")]
    [Authorize]
    public async Task<IActionResult> BackfillTmdb(int userId, CancellationToken cancellationToken)
    {
        if (!IsCurrentUser(userId))
        {
            return Forbid();
        }

        if (!await _context.Auth.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return NotFound(new { message = "Usuário não encontrado." });
        }

        var resolved = await _letterboxdService.BackfillTmdbIdsAsync(userId, cancellationToken);
        return Ok(new
        {
            data = new { resolved },
            message = $"{resolved} filme(s) vinculado(s) ao TMDB."
        });
    }

    private bool IsCurrentUser(int userId)
    {
        var claimValue = User.FindFirst(ClaimTypes.Name)?.Value;
        return int.TryParse(claimValue, out var authenticatedUserId) && authenticatedUserId == userId;
    }
}

public sealed record ConnectLetterboxdRequest(string? Username);
