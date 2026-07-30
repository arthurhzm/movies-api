using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;
using MoviesAPI.DTO;
using MoviesAPI.Models;

namespace MoviesAPI.Services;

public class MatchService
{
    private readonly AppDbContext _context;
    private readonly HuggingFaceService _huggingFaceService;
    private readonly ITmdbResolver _tmdbResolver;

    public MatchService(AppDbContext context, HuggingFaceService huggingFaceService, ITmdbResolver tmdbResolver)
    {
        _context = context;
        _huggingFaceService = huggingFaceService;
        _tmdbResolver = tmdbResolver;
    }

    public async Task<MatchResultDTO> GenerateMatchAsync(int userId1, int userId2)
    {
        // DbContext não suporta consultas concorrentes na mesma instância.
        var prefs1 = await _context.UserPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId1);
        var prefs2 = await _context.UserPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId2);
        var movies1 = await _context.UserMovieFeedback.AsNoTracking().Where(f => f.UserId == userId1).ToListAsync();
        var movies2 = await _context.UserMovieFeedback.AsNoTracking().Where(f => f.UserId == userId2).ToListAsync();
        var user1 = await _context.Auth.AsNoTracking().FirstOrDefaultAsync(user => user.Id == userId1);
        var user2 = await _context.Auth.AsNoTracking().FirstOrDefaultAsync(user => user.Id == userId2);

        // Find intersections
        var commonGenres = prefs1 != null && prefs2 != null
            ? prefs1.FavoriteGenres.Intersect(prefs2.FavoriteGenres, StringComparer.OrdinalIgnoreCase).ToList()
            : new List<string>();

        var commonDirectors = prefs1 != null && prefs2 != null
            ? prefs1.FavoriteDirectors.Intersect(prefs2.FavoriteDirectors, StringComparer.OrdinalIgnoreCase).ToList()
            : new List<string>();

        var allWatched = movies1.Select(m => m.MovieTitle)
            .Union(movies2.Select(m => m.MovieTitle), StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Sets to reject already-watched picks (by normalized title AND stable tmdbId).
        var watchedTitles = new HashSet<string>(
            movies1.Concat(movies2).Select(m => NormalizeTitle(m.MovieTitle)),
            StringComparer.Ordinal);
        var watchedTmdbIds = new HashSet<int>(
            movies1.Concat(movies2).Where(m => m.TmdbId.HasValue).Select(m => m.TmdbId!.Value));

        // Generate, then reject any movie either user already saw and retry while
        // telling the model to avoid it. Match is a single-item generation (fast),
        // so a few attempts stay well within the request budget.
        var avoid = new List<string>();
        MatchResultDTO result;
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            var prompt = BuildMatchPrompt(
                user1?.Username ?? $"Usuario{userId1}",
                user2?.Username ?? $"Usuario{userId2}",
                prefs1, prefs2,
                movies1, movies2,
                commonGenres, commonDirectors,
                allWatched, avoid);

            var responseText = await _huggingFaceService.GenerateStructuredJsonAsync(
                prompt,
                "movie_match",
                HuggingFaceJsonSchemas.Match);
            result = ParseMatchJson(responseText, commonGenres, commonDirectors);

            // Resolve the picked movie to its stable TMDB id so the frontend can load
            // the poster/details reliably (best-effort; stays null if not found).
            result.TmdbId = await _tmdbResolver.ResolveTmdbIdAsync(result.MovieTitle, result.Year);

            var alreadyWatched = watchedTitles.Contains(NormalizeTitle(result.MovieTitle))
                || (result.TmdbId.HasValue && watchedTmdbIds.Contains(result.TmdbId.Value));
            if (!alreadyWatched || attempt >= maxAttempts)
            {
                break;
            }

            avoid.Add(result.MovieTitle);
        }

        // Save to history
        _context.Matches.Add(new MatchModel
        {
            UserId1 = userId1,
            UserId2 = userId2,
            MovieTitle = result.MovieTitle,
            TmdbId = result.TmdbId,
            Year = result.Year,
            WhyItWorks = result.WhyItWorks,
            CompatibilityScore = result.CompatibilityScore,
            CommonGenres = JsonSerializer.Serialize(result.CommonGenres),
            GeneratedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        return result;
    }

    public async Task<List<MatchHistoryDTO>> GetMatchHistoryAsync(int userId)
    {
        var matches = await _context.Matches
            .Where(m => m.UserId1 == userId || m.UserId2 == userId)
            .OrderByDescending(m => m.GeneratedAt)
            .Take(20)
            .ToListAsync();

        return matches.Select(m =>
        {
            List<string> genres = new();
            if (!string.IsNullOrEmpty(m.CommonGenres))
            {
                try { genres = JsonSerializer.Deserialize<List<string>>(m.CommonGenres) ?? new(); }
                catch { }
            }
            return new MatchHistoryDTO
            {
                Id = m.Id,
                UserId1 = m.UserId1,
                UserId2 = m.UserId2,
                MovieTitle = m.MovieTitle,
                TmdbId = m.TmdbId,
                Year = m.Year,
                WhyItWorks = m.WhyItWorks,
                CompatibilityScore = m.CompatibilityScore,
                CommonGenres = genres,
                GeneratedAt = m.GeneratedAt
            };
        }).ToList();
    }

    private MatchResultDTO ParseMatchJson(string json, List<string> commonGenres, List<string> commonDirectors)
    {
        try
        {
            var cleaned = json.Replace("```json", "").Replace("```", "").Trim();
            using var doc = JsonDocument.Parse(cleaned);
            var el = doc.RootElement;

            return new MatchResultDTO
            {
                MovieTitle = el.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                Year = el.TryGetProperty("year", out var y) && y.TryGetInt32(out var yi) ? yi : null,
                WhyItWorks = el.TryGetProperty("why_it_works", out var w) ? w.GetString() : null,
                CompatibilityScore = el.TryGetProperty("compatibility_score", out var cs) && cs.TryGetInt32(out var csi) ? csi : 80,
                CommonGenres = commonGenres,
                CommonDirectors = commonDirectors
            };
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw new HuggingFaceGenerationException("O modelo retornou uma combinação inválida.", ex);
        }
    }

    private static string NormalizeTitle(string title) =>
        string.Join(' ', (title ?? string.Empty).Trim().ToUpperInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private string BuildMatchPrompt(
        string username1, string username2,
        UserPreferencesModel? prefs1, UserPreferencesModel? prefs2,
        List<UserMovieFeedbackModel> movies1, List<UserMovieFeedbackModel> movies2,
        List<string> commonGenres, List<string> commonDirectors,
        List<string> allWatched, IReadOnlyList<string> avoid)
    {
        var avoidLine = avoid.Count > 0
            ? $"\n- Você JÁ sugeriu estes e foram recusados por já terem sido vistos; escolha outro: {string.Join(", ", avoid)}"
            : string.Empty;

        var msg = $@"Você é um cinéfilo especialista. Dois usuários querem assistir um filme JUNTOS.
OBJETIVO: recomendar UM filme que NENHUM dos dois já assistiu, mas que ambos provavelmente vão gostar,
com base no gosto em comum (gêneros/diretores em comum e o estilo dos filmes que cada um avaliou bem).
NÃO recomende um filme que qualquer um dos dois já tenha visto.

USUÁRIO 1 ({username1}):
- Gêneros favoritos: {(prefs1 != null ? string.Join(", ", prefs1.FavoriteGenres) : "não informado")}
- Diretores favoritos: {(prefs1 != null ? string.Join(", ", prefs1.FavoriteDirectors) : "não informado")}
- Filmes bem avaliados: {string.Join(", ", movies1.Where(m => m.Rating >= 4).Select(m => $"{m.MovieTitle} ({m.Rating}★)").Take(40))}

USUÁRIO 2 ({username2}):
- Gêneros favoritos: {(prefs2 != null ? string.Join(", ", prefs2.FavoriteGenres) : "não informado")}
- Diretores favoritos: {(prefs2 != null ? string.Join(", ", prefs2.FavoriteDirectors) : "não informado")}
- Filmes bem avaliados: {string.Join(", ", movies2.Where(m => m.Rating >= 4).Select(m => $"{m.MovieTitle} ({m.Rating}★)").Take(40))}

INTERSEÇÕES:
- Gêneros em comum: {string.Join(", ", commonGenres)}
- Diretores em comum: {string.Join(", ", commonDirectors)}

REGRA OBRIGATÓRIA — filmes JÁ ASSISTIDOS por um dos dois (NÃO recomende NENHUM destes):
{string.Join(", ", allWatched.Take(200))}{avoidLine}

Retorne um único JSON com:
- title (título em português do Brasil se existir, senão original)
- year
- why_it_works (por que este filme funciona para os dois, máx 300 chars)
- compatibility_score (0-100, quão bem o filme serve os dois perfis)

Retorne apenas JSON válido, sem markdown.";

        return msg;
    }
}
