using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;
using MoviesAPI.DTO;
using MoviesAPI.Models;

namespace MoviesAPI.Services;

public class RecommendationService
{
    private readonly AppDbContext _context;
    private readonly GeminiService _geminiService;
    private const int CacheMinutes = 10;

    public RecommendationService(AppDbContext context, GeminiService geminiService)
    {
        _context = context;
        _geminiService = geminiService;
    }

    // ─── Cache invalidation ──────────────────────────────────────────────────

    public async Task InvalidateCacheAsync(int userId)
    {
        var cached = await _context.GeneratedRecommendations
            .Where(r => r.UserId == userId)
            .ToListAsync();

        if (cached.Count > 0)
        {
            _context.GeneratedRecommendations.RemoveRange(cached);
            await _context.SaveChangesAsync();
        }
    }

    // ─── Main entrypoint ─────────────────────────────────────────────────────

    public async Task<List<RecommendationDTO>> GetRecommendationsAsync(int userId, bool special = false)
    {
        // Check cache
        var since = DateTime.UtcNow.AddMinutes(-CacheMinutes);
        var cached = await _context.GeneratedRecommendations
            .Where(r => r.UserId == userId && r.IsSpecial == special && r.GeneratedAt >= since)
            .OrderByDescending(r => r.GeneratedAt)
            .ToListAsync();

        if (cached.Count > 0)
            return cached.Select(MapToDTO).ToList();

        // Fetch all user context in parallel
        var (preferences, watchedMovies, recFeedback, friendsMovies) =
            await FetchUserContextAsync(userId);

        var prompt = BuildPrompt(preferences, watchedMovies, recFeedback, friendsMovies, 10, special);
        var responseText = await _geminiService.GenerateContentAsync(prompt);
        var recommendations = ParseJson(responseText, special);

        // Persist to cache (replace old entries)
        var toDelete = await _context.GeneratedRecommendations
            .Where(r => r.UserId == userId && r.IsSpecial == special)
            .ToListAsync();
        _context.GeneratedRecommendations.RemoveRange(toDelete);

        foreach (var rec in recommendations)
        {
            _context.GeneratedRecommendations.Add(new GeneratedRecommendationModel
            {
                UserId = userId,
                MovieTitle = rec.MovieTitle,
                Year = rec.Year,
                WhyRecommend = rec.WhyRecommend,
                StreamingServices = JsonSerializer.Serialize(rec.StreamingServices),
                IsSpecial = special,
                GeneratedAt = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync();

        return recommendations;
    }

    // ─── Data fetching ────────────────────────────────────────────────────────

    private async Task<(
        UserPreferencesModel?,
        List<UserMovieFeedbackModel>,
        List<UserRecommendationFeedbackModel>,
        List<(string MovieTitle, int FriendCount, double AvgRating)>
    )> FetchUserContextAsync(int userId)
    {
        var prefsTask = _context.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId);

        var moviesTask = _context.UserMovieFeedback
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.Rating)
            .ThenByDescending(f => f.UpdatedAt)
            .Take(150)
            .ToListAsync();

        var recFeedbackTask = _context.UserRecommendationFeedback
            .Where(f => f.UserId == userId)
            .ToListAsync();

        // People the user follows
        var followingIdsTask = _context.UserFollowers
            .Where(f => f.FollowerId == userId)
            .Select(f => f.UserId)
            .ToListAsync();

        await Task.WhenAll(prefsTask, moviesTask, recFeedbackTask, followingIdsTask);

        var followingIds = await followingIdsTask;

        // Top-rated movies from friends (excluding already watched by this user)
        var userWatchedTitles = (await moviesTask).Select(m => m.MovieTitle).ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<(string, int, double)> friendsMovies = new();
        if (followingIds.Count > 0)
        {
            friendsMovies = await _context.UserMovieFeedback
                .Where(f => followingIds.Contains(f.UserId) && f.Rating >= 4
                            && !userWatchedTitles.Contains(f.MovieTitle))
                .GroupBy(f => f.MovieTitle)
                .Select(g => new
                {
                    MovieTitle = g.Key,
                    FriendCount = g.Count(),
                    AvgRating = g.Average(m => m.Rating)
                })
                .OrderByDescending(g => g.FriendCount)
                .ThenByDescending(g => g.AvgRating)
                .Take(20)
                .ToListAsync()
                .ContinueWith(t => t.Result.Select(x => (x.MovieTitle, x.FriendCount, x.AvgRating)).ToList());
        }

        return (await prefsTask, await moviesTask, await recFeedbackTask, friendsMovies);
    }

    // ─── Prompt builder ───────────────────────────────────────────────────────

    private string BuildPrompt(
        UserPreferencesModel? prefs,
        List<UserMovieFeedbackModel> watched,
        List<UserRecommendationFeedbackModel> recFeedback,
        List<(string MovieTitle, int FriendCount, double AvgRating)> friendsMovies,
        int count,
        bool special)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Você é um cinéfilo especialista. Seu objetivo é recomendar filmes com precisão cirúrgica para este usuário específico.");
        sb.AppendLine("Leia TODO o contexto abaixo antes de recomendar qualquer coisa.\n");

        // ── Declared preferences ──
        if (prefs != null)
        {
            sb.AppendLine("## PREFERÊNCIAS DECLARADAS");
            if (prefs.FavoriteGenres.Count > 0)
                sb.AppendLine($"- Gêneros: {string.Join(", ", prefs.FavoriteGenres)}");
            if (prefs.FavoriteDirectors.Count > 0)
                sb.AppendLine($"- Diretores favoritos: {string.Join(", ", prefs.FavoriteDirectors)}");
            if (prefs.FavoriteActors.Count > 0)
                sb.AppendLine($"- Atores favoritos: {string.Join(", ", prefs.FavoriteActors)}");
            if (prefs.MinReleaseYear.HasValue)
                sb.AppendLine($"- Período: filmes a partir de {prefs.MinReleaseYear}");
            if (prefs.MaxDuration.HasValue)
                sb.AppendLine($"- Duração máxima: {prefs.MaxDuration} minutos");
            if (!prefs.AcceptAdultContent)
                sb.AppendLine("- Não aceita conteúdo adulto explícito");
            sb.AppendLine();
        }

        // ── Watch history ──
        if (watched.Count > 0)
        {
            var loved    = watched.Where(m => m.Rating >= 4.5).ToList();
            var liked    = watched.Where(m => m.Rating >= 3.5 && m.Rating < 4.5).ToList();
            var disliked = watched.Where(m => m.Rating > 0 && m.Rating < 3.5).ToList();
            var marked   = watched.Where(m => m.Rating == 0).ToList();

            sb.AppendLine("## HISTÓRICO DE FILMES ASSISTIDOS");
            sb.AppendLine("(Use estes dados para identificar padrões reais de gosto — têm prioridade sobre as preferências declaradas)\n");

            if (loved.Count > 0)
            {
                sb.AppendLine("### AMADOS (4.5–5★) — analise profundamente o que têm em comum:");
                foreach (var m in loved)
                {
                    var line = $"- \"{m.MovieTitle}\" ({m.Rating}★)";
                    if (!string.IsNullOrWhiteSpace(m.Review))
                        line += $" — \"{m.Review.Trim()}\"";
                    sb.AppendLine(line);
                }
                sb.AppendLine();
            }

            if (liked.Count > 0)
            {
                sb.AppendLine("### BEM AVALIADOS (3.5–4★):");
                foreach (var m in liked.Take(30))
                {
                    var line = $"- \"{m.MovieTitle}\" ({m.Rating}★)";
                    if (!string.IsNullOrWhiteSpace(m.Review))
                        line += $" — \"{m.Review.Trim()}\"";
                    sb.AppendLine(line);
                }
                sb.AppendLine();
            }

            if (disliked.Count > 0)
            {
                sb.AppendLine("### REJEITADOS (abaixo de 3.5★) — EVITE características similares:");
                foreach (var m in disliked.Take(20))
                {
                    var line = $"- \"{m.MovieTitle}\" ({m.Rating}★)";
                    if (!string.IsNullOrWhiteSpace(m.Review))
                        line += $" — \"{m.Review.Trim()}\"";
                    sb.AppendLine(line);
                }
                sb.AppendLine();
            }

            if (marked.Count > 0)
            {
                sb.AppendLine("### ASSISTIDOS SEM NOTA (não recomendar novamente):");
                sb.AppendLine(string.Join(", ", marked.Take(30).Select(m => $"\"{m.MovieTitle}\"")));
                sb.AppendLine();
            }

            sb.AppendLine($"**NUNCA recomende nenhum dos {watched.Count} filmes acima — o usuário já assistiu a todos.**\n");
        }

        // ── Recommendation feedback ──
        if (recFeedback.Count > 0)
        {
            var superliked = recFeedback.Where(r => r.Feedback == "superlike").ToList();
            var likedRecs  = recFeedback.Where(r => r.Feedback == "like").ToList();
            var dislikedRecs = recFeedback.Where(r => r.Feedback == "dislike").ToList();

            sb.AppendLine("## HISTÓRICO DE RECOMENDAÇÕES ANTERIORES");
            sb.AppendLine("(Feedback sobre filmes que foram recomendados pela IA anteriormente)\n");

            if (superliked.Count > 0)
            {
                sb.AppendLine("### SUPER CURTIDAS — o usuário ADOROU estas recomendações, entenda por quê:");
                foreach (var r in superliked)
                {
                    var line = $"- \"{r.MovieTitle}\"";
                    if (!string.IsNullOrWhiteSpace(r.DetailedFeedback))
                        line += $" — \"{r.DetailedFeedback.Trim()}\"";
                    sb.AppendLine(line);
                }
                sb.AppendLine();
            }

            if (likedRecs.Count > 0)
            {
                sb.AppendLine("### ACEITAS:");
                foreach (var r in likedRecs)
                {
                    var line = $"- \"{r.MovieTitle}\"";
                    if (!string.IsNullOrWhiteSpace(r.DetailedFeedback))
                        line += $" — \"{r.DetailedFeedback.Trim()}\"";
                    sb.AppendLine(line);
                }
                sb.AppendLine();
            }

            if (dislikedRecs.Count > 0)
            {
                sb.AppendLine("### REJEITADAS — NÃO repita este tipo de recomendação:");
                foreach (var r in dislikedRecs)
                {
                    var line = $"- \"{r.MovieTitle}\"";
                    if (!string.IsNullOrWhiteSpace(r.DetailedFeedback))
                        line += $" — \"{r.DetailedFeedback.Trim()}\"";
                    sb.AppendLine(line);
                }
                sb.AppendLine();
            }

            sb.AppendLine($"**NUNCA recomende novamente: {string.Join(", ", recFeedback.Select(r => $"\"{r.MovieTitle}\""))}**\n");
        }

        // ── Friends' signals ──
        if (friendsMovies.Count > 0)
        {
            sb.AppendLine("## SINAL DOS AMIGOS");
            sb.AppendLine("(Filmes que as pessoas que este usuário segue avaliaram muito bem — sinal secundário de qualidade)\n");
            foreach (var (title, friendCount, avgRating) in friendsMovies.Take(15))
            {
                var who = friendCount == 1 ? "1 amigo" : $"{friendCount} amigos";
                sb.AppendLine($"- \"{title}\" — amado por {who} (média {avgRating:F1}★)");
            }
            sb.AppendLine();
        }

        // ── Analysis instructions ──
        sb.AppendLine("## INSTRUÇÕES DE ANÁLISE (execute antes de recomendar)");
        sb.AppendLine();
        sb.AppendLine("1. **Identifique padrões reais** a partir dos filmes AMADOS:");
        sb.AppendLine("   - Diretores e atores recorrentes");
        sb.AppendLine("   - Temáticas, gêneros e subgêneros reais (não apenas os declarados)");
        sb.AppendLine("   - Estilos visuais, ritmo narrativo, tom emocional");
        sb.AppendLine("   - Décadas e países de origem");
        sb.AppendLine("   - O que as reviews dizem sobre o que o usuário realmente valorizou");
        sb.AppendLine();
        sb.AppendLine("2. **Identifique o que o usuário REJEITA** a partir dos filmes mal avaliados e recomendações rejeitadas:");
        sb.AppendLine("   - Elementos narrativos, tipos de personagem, estilos que ele não gosta");
        sb.AppendLine();
        sb.AppendLine("3. **Use as super curtidas como calibração**: se uma recomendação anterior foi super curtida, entenda o que ela tinha e replique esse padrão.");
        sb.AppendLine();
        sb.AppendLine("4. **Considere os amigos**: se um filme foi muito bem avaliado por vários amigos E combina com o perfil, dê peso extra.");
        sb.AppendLine();
        sb.AppendLine("5. **Diversifique inteligentemente**: não repita o mesmo diretor ou temática nas 10 recomendações. Inclua 1 filme surpresa coerente com o perfil.");
        sb.AppendLine();

        // ── Output format ──
        sb.AppendLine("## TAREFA");
        sb.AppendLine();
        sb.AppendLine($"Recomende {count} filmes. Para cada um, o campo `why_recommend` deve:");
        sb.AppendLine("- Fazer referência DIRETA ao histórico deste usuário (ex: 'Assim como você amou Interstellar pela complexidade narrativa...')");
        sb.AppendLine("- Explicar especificamente o que ESTE filme tem que combina com ESTE perfil");
        sb.AppendLine("- Ter no máximo 250 caracteres");
        sb.AppendLine();
        sb.AppendLine("Retorne APENAS o JSON válido abaixo, sem markdown, sem texto extra:");
        sb.AppendLine();
        sb.AppendLine("[");
        sb.AppendLine("  {");
        sb.AppendLine("    \"title\": \"Nome do Filme\",");
        sb.AppendLine("    \"year\": 2001,");
        sb.AppendLine("    \"why_recommend\": \"Referência direta ao perfil do usuário...\",");
        sb.AppendLine("    \"streaming_services\": [\"Netflix\", \"Prime Video\"]");
        sb.AppendLine("  }");
        sb.AppendLine("]");
        sb.AppendLine();
        sb.AppendLine("Regras adicionais:");
        sb.AppendLine("- Títulos em português do Brasil quando existir tradução oficial");
        sb.AppendLine("- streaming_services: apenas serviços disponíveis no Brasil; array vazio se não souber");
        sb.AppendLine("- Não inclua filmes dos últimos 3 meses (pode ter informação desatualizada)");

        if (special)
            sb.Append(BuildSpecialSection());

        return sb.ToString();
    }

    private static string BuildSpecialSection()
    {
        var now = DateTime.Now;
        var month = now.Month;
        var day = now.Day;

        var sb = new StringBuilder();
        sb.AppendLine("\n## MODO ESPECIAL — RECOMENDAÇÃO TEMÁTICA");
        sb.AppendLine($"Data atual: {now:dd/MM/yyyy}");
        sb.AppendLine("IGNORE completamente as preferências e histórico do usuário. Recomende baseado APENAS na relevância cultural/temporal do momento.\n");

        string theme;
        if ((month == 12 && day >= 15) || (month == 1 && day <= 7))
            theme = "Natal e Ano Novo. Recomende filmes natalinos clássicos e contemporâneos, filmes de reflexão de fim de ano, obras sobre família, esperança e renovação.";
        else if (month == 10 && day >= 15)
            theme = "Halloween. Recomende filmes de terror, suspense e horror variado: sobrenatural, slasher, psicológico, body horror. Mix de clássicos e recentes.";
        else if (month == 2 && day >= 10 && day <= 16)
            theme = "Dia dos Namorados. Recomende romances variados: clássico, comédia romântica, drama, LGBTQ+. Inclua atemporais e lançamentos recentes.";
        else if (month == 6 && day >= 20 && day <= 30)
            theme = "Festa Junina. Recomende filmes brasileiros sobre cultura regional, vida no interior e tradições populares.";
        else if (month == 9 && day >= 1 && day <= 7)
            theme = "Independência do Brasil. Recomende filmes sobre história brasileira, lutas por independência e cinema nacional premiado.";
        else if (month == 3 && day >= 5 && day <= 10)
            theme = "Dia Internacional da Mulher. Recomende filmes dirigidos por mulheres, protagonistas femininas fortes e obras sobre empoderamento.";
        else if (month == 4 && day >= 18 && day <= 25)
            theme = "Semana dos Povos Indígenas. Recomende filmes sobre culturas indígenas, preservação ambiental e povos originários.";
        else if (month >= 6 && month <= 8)
            theme = "Inverno (Hemisfério Sul). Recomende filmes para maratonar em casa: dramas envolventes, thrillers, filmes de época e obras contemplativas.";
        else if (month == 12 || month <= 2)
            theme = "Verão (Hemisfério Sul). Recomende filmes leves: comédias, aventuras, ação e documentários inspiradores para maratonar em família.";
        else
            theme = "Sem data comemorativa específica. Recomende baseado em: filmes premiados recentemente, lançamentos com impacto cultural, diretores em evidência. Priorize diversidade de países e décadas.";

        sb.AppendLine($"Tema: {theme}");
        return sb.ToString();
    }

    // ─── JSON parser ──────────────────────────────────────────────────────────

    private static List<RecommendationDTO> ParseJson(string json, bool special)
    {
        try
        {
            var cleaned = json.Replace("```json", "").Replace("```", "").Trim();
            using var doc = JsonDocument.Parse(cleaned);
            var list = new List<RecommendationDTO>();

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var rec = new RecommendationDTO
                {
                    MovieTitle = el.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                    Year       = el.TryGetProperty("year",  out var y) && y.TryGetInt32(out var yi) ? yi : null,
                    WhyRecommend = el.TryGetProperty("why_recommend", out var w) ? w.GetString() : null,
                    IsSpecial  = special
                };

                if (el.TryGetProperty("streaming_services", out var ss))
                    rec.StreamingServices = ss.EnumerateArray()
                        .Select(s => s.GetString() ?? "")
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();

                list.Add(rec);
            }
            return list;
        }
        catch
        {
            return new List<RecommendationDTO>();
        }
    }

    // ─── Mapper ───────────────────────────────────────────────────────────────

    private static RecommendationDTO MapToDTO(GeneratedRecommendationModel model)
    {
        List<string> streaming = new();
        if (!string.IsNullOrEmpty(model.StreamingServices))
        {
            try { streaming = JsonSerializer.Deserialize<List<string>>(model.StreamingServices) ?? new(); }
            catch { }
        }
        return new RecommendationDTO
        {
            MovieTitle       = model.MovieTitle,
            Year             = model.Year,
            WhyRecommend     = model.WhyRecommend,
            StreamingServices = streaming,
            IsSpecial        = model.IsSpecial
        };
    }
}
