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

    public MatchService(AppDbContext context, HuggingFaceService huggingFaceService)
    {
        _context = context;
        _huggingFaceService = huggingFaceService;
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

        var prompt = BuildMatchPrompt(
            user1?.Username ?? $"Usuario{userId1}",
            user2?.Username ?? $"Usuario{userId2}",
            prefs1, prefs2,
            movies1, movies2,
            commonGenres, commonDirectors,
            allWatched);

        var responseText = await _huggingFaceService.GenerateStructuredJsonAsync(
            prompt,
            "movie_match",
            HuggingFaceJsonSchemas.Match);
        var result = ParseMatchJson(responseText, commonGenres, commonDirectors);

        // Save to history
        _context.Matches.Add(new MatchModel
        {
            UserId1 = userId1,
            UserId2 = userId2,
            MovieTitle = result.MovieTitle,
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

    private string BuildMatchPrompt(
        string username1, string username2,
        UserPreferencesModel? prefs1, UserPreferencesModel? prefs2,
        List<UserMovieFeedbackModel> movies1, List<UserMovieFeedbackModel> movies2,
        List<string> commonGenres, List<string> commonDirectors,
        List<string> allWatched)
    {
        var msg = $@"Você é um cinéfilo especialista. Dois usuários querem assistir um filme juntos.
Encontre o filme PERFEITO para eles assistirem juntos.

USUÁRIO 1 ({username1}):
- Gêneros favoritos: {(prefs1 != null ? string.Join(", ", prefs1.FavoriteGenres) : "não informado")}
- Diretores favoritos: {(prefs1 != null ? string.Join(", ", prefs1.FavoriteDirectors) : "não informado")}
- Filmes bem avaliados: {string.Join(", ", movies1.Where(m => m.Rating >= 4).Select(m => $"{m.MovieTitle} ({m.Rating}★)"))}

USUÁRIO 2 ({username2}):
- Gêneros favoritos: {(prefs2 != null ? string.Join(", ", prefs2.FavoriteGenres) : "não informado")}
- Diretores favoritos: {(prefs2 != null ? string.Join(", ", prefs2.FavoriteDirectors) : "não informado")}
- Filmes bem avaliados: {string.Join(", ", movies2.Where(m => m.Rating >= 4).Select(m => $"{m.MovieTitle} ({m.Rating}★)"))}

INTERSEÇÕES:
- Gêneros em comum: {string.Join(", ", commonGenres)}
- Diretores em comum: {string.Join(", ", commonDirectors)}

RESTRIÇÕES:
- NÃO recomende nenhum destes filmes (já assistidos): {string.Join(", ", allWatched.Take(50))}

Retorne um único JSON com:
- title (título em português do Brasil se existir, senão original)
- year
- why_it_works (por que este filme funciona para os dois, máx 300 chars)
- compatibility_score (0-100, quão bem o filme serve os dois perfis)

Retorne apenas JSON válido, sem markdown.";

        return msg;
    }
}
