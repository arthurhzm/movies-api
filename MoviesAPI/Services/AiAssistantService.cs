using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;
using MoviesAPI.DTO;

namespace MoviesAPI.Services;

public sealed class AiAssistantService
{
    private readonly AppDbContext _context;
    private readonly HuggingFaceService _huggingFaceService;

    public AiAssistantService(AppDbContext context, HuggingFaceService huggingFaceService)
    {
        _context = context;
        _huggingFaceService = huggingFaceService;
    }

    public async Task<List<AiMovieSearchResultDTO>> SearchMoviesAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"""
            Você é um especialista em cinema e busca semântica. Encontre até 6 filmes reais que correspondam à consulta abaixo.
            Priorize títulos exatos e depois relações temáticas genuínas. Não invente filmes ou disponibilidade em streaming.
            Mantenha cada "overview" curto, com no máximo 240 caracteres.

            Consulta: {query}
            """;
        var response = await _huggingFaceService.GenerateStructuredJsonAsync(
            prompt,
            "movie_search_results",
            HuggingFaceJsonSchemas.MovieSearch,
            cancellationToken);

        return DeserializeRequired<List<AiMovieSearchResultDTO>>(response, "resultados de busca");
    }

    public async Task<string> GenerateChatResponseAsync(
        int userId,
        IReadOnlyList<AiChatMessageRequest> messages,
        CancellationToken cancellationToken = default)
    {
        var preferences = await _context.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(preference => preference.UserId == userId, cancellationToken);
        var watched = await _context.UserMovieFeedback
            .AsNoTracking()
            .Where(feedback => feedback.UserId == userId)
            .OrderByDescending(feedback => feedback.Rating)
            .ThenByDescending(feedback => feedback.UpdatedAt)
            .Take(60)
            .ToListAsync(cancellationToken);
        var recommendationFeedback = await _context.UserRecommendationFeedback
            .AsNoTracking()
            .Where(feedback => feedback.UserId == userId)
            .Take(60)
            .ToListAsync(cancellationToken);

        var transcript = new StringBuilder();
        foreach (var message in messages.TakeLast(20))
        {
            var speaker = string.Equals(message.Sender, "ai", StringComparison.OrdinalIgnoreCase)
                ? "Assistente"
                : "Usuário";
            transcript.AppendLine($"{speaker}: {message.Text!.Trim()}");
        }

        var prompt = $"""
            Você é o assistente cinéfilo do CineMatch. Fale exclusivamente sobre filmes e séries de forma amigável.
            Responda em português do Brasil. Use apenas HTML simples permitido na interface: p, strong, em, ul, li e a.
            Quando sugerir um filme, use o link <a href="/search?query=TITULO" className="text-primary">TITULO</a>.

            PERFIL DO USUÁRIO
            - Gêneros favoritos: {JoinOrFallback(preferences?.FavoriteGenres)}
            - Diretores favoritos: {JoinOrFallback(preferences?.FavoriteDirectors)}
            - Atores favoritos: {JoinOrFallback(preferences?.FavoriteActors)}
            - Filmes muito bem avaliados: {JoinOrFallback(watched.Where(movie => movie.Rating >= 4.5).Select(movie => movie.MovieTitle))}
            - Filmes rejeitados: {JoinOrFallback(watched.Where(movie => movie.Rating > 0 && movie.Rating < 3.5).Select(movie => movie.MovieTitle))}
            - Recomendações rejeitadas: {JoinOrFallback(recommendationFeedback.Where(feedback => feedback.Feedback == "dislike").Select(feedback => feedback.MovieTitle))}

            CONVERSA
            {transcript}
            """;

        return await _huggingFaceService.GenerateConversationAsync(prompt, cancellationToken);
    }

    private static T DeserializeRequired<T>(string json, string responseName)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new JsonException("Resposta vazia.");
        }
        catch (JsonException ex)
        {
            throw new HuggingFaceGenerationException($"O modelo retornou {responseName} inválidos.", ex);
        }
    }

    private static string JoinOrFallback(IEnumerable<string>? values) =>
        values is null || !values.Any() ? "não informado" : string.Join(", ", values.Take(30));
}
