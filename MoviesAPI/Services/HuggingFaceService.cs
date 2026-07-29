using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MoviesAPI.Services;

public sealed class HuggingFaceGenerationException : Exception
{
    public HuggingFaceGenerationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class HuggingFaceService
{
    private const string DefaultRecommendationModel = "Qwen/Qwen3-32B";
    private const string DefaultConversationModel = "openai/gpt-oss-120b";

    private readonly HttpClient _httpClient;
    private readonly ILogger<HuggingFaceService> _logger;
    private readonly string _token;
    private readonly string _recommendationModel;
    private readonly string _conversationModel;

    public HuggingFaceService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<HuggingFaceService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("HuggingFaceClient");
        _logger = logger;
        _token = Environment.GetEnvironmentVariable("HF_TOKEN")
            ?? configuration["HuggingFace:Token"]
            ?? throw new InvalidOperationException("HF_TOKEN is not configured");
        _recommendationModel = configuration["HuggingFace:RecommendationModel"] ?? DefaultRecommendationModel;
        _conversationModel = configuration["HuggingFace:ConversationModel"] ?? DefaultConversationModel;
    }

    public Task<string> GenerateStructuredJsonAsync(
        string prompt,
        string schemaName,
        object schema,
        CancellationToken cancellationToken = default) =>
        SendChatCompletionAsync(
            _recommendationModel,
            prompt,
            new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = schemaName,
                    strict = true,
                    schema
                }
            },
            maxTokens: 2_000,
            cancellationToken);

    public Task<string> GenerateConversationAsync(
        string prompt,
        CancellationToken cancellationToken = default) =>
        SendChatCompletionAsync(
            _conversationModel,
            prompt,
            responseFormat: null,
            maxTokens: 800,
            cancellationToken);

    private async Task<string> SendChatCompletionAsync(
        string model,
        string prompt,
        object? responseFormat,
        int maxTokens,
        CancellationToken cancellationToken)
    {
        var messages = new[]
        {
            new
            {
                role = "system",
                content = "Você é o motor de IA do CineMatch. Siga rigorosamente o formato solicitado e responda em português do Brasil quando aplicável."
            },
            new { role = "user", content = prompt }
        };
        object requestBody = responseFormat is null
            ? new { model, messages, temperature = 0.35, max_tokens = maxTokens }
            : new { model, messages, temperature = 0.35, max_tokens = maxTokens, response_format = responseFormat };

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Hugging Face request failed for model {Model}", model);
            throw new HuggingFaceGenerationException("Não foi possível alcançar o provedor de IA.", ex);
        }

        using (response)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Hugging Face generation failed with status {StatusCode} for model {Model}",
                    (int)response.StatusCode,
                    model);
                throw new HuggingFaceGenerationException("Não foi possível gerar a resposta agora.");
            }

            try
            {
                using var document = JsonDocument.Parse(responseBody);
                var content = document.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                return string.IsNullOrWhiteSpace(content)
                    ? throw new HuggingFaceGenerationException("O modelo não retornou conteúdo.")
                    : content;
            }
            catch (HuggingFaceGenerationException)
            {
                throw;
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Hugging Face returned an invalid chat completion payload for model {Model}", model);
                throw new HuggingFaceGenerationException("O provedor de IA retornou uma resposta inválida.", ex);
            }
        }
    }
}

internal static class HuggingFaceJsonSchemas
{
    public static readonly object Recommendations = new
    {
        type = "array",
        items = new
        {
            type = "object",
            properties = new
            {
                title = new { type = "string" },
                year = new { type = "integer" },
                why_recommend = new { type = "string" },
                streaming_services = new
                {
                    type = "array",
                    items = new { type = "string" }
                }
            },
            required = new[] { "title", "year", "why_recommend", "streaming_services" },
            additionalProperties = false
        }
    };

    public static readonly object Match = new
    {
        type = "object",
        properties = new
        {
            title = new { type = "string" },
            year = new { type = "integer" },
            why_it_works = new { type = "string" },
            compatibility_score = new { type = "integer" }
        },
        required = new[] { "title", "year", "why_it_works", "compatibility_score" },
        additionalProperties = false
    };

    public static readonly object Roulette = new
    {
        type = "array",
        items = new
        {
            type = "object",
            properties = new
            {
                title = new { type = "string" },
                year = new { type = "integer" },
                overview = new { type = "string" },
                why_recommend = new { type = "string" },
                streaming_services = new
                {
                    type = "array",
                    items = new { type = "string" }
                },
                confidence_score = new { type = "integer" },
                perfect_match_reasons = new
                {
                    type = "array",
                    items = new { type = "string" }
                }
            },
            required = new[]
            {
                "title", "year", "overview", "why_recommend", "streaming_services", "confidence_score", "perfect_match_reasons"
            },
            additionalProperties = false
        }
    };

    public static readonly object MovieSearch = new
    {
        type = "array",
        items = new
        {
            type = "object",
            properties = new
            {
                title = new { type = "string" },
                year = new { type = "integer" },
                genres = new
                {
                    type = "array",
                    items = new { type = "string" }
                },
                overview = new { type = "string" },
                streaming_services = new
                {
                    type = "array",
                    items = new { type = "string" }
                }
            },
            required = new[] { "title", "year", "genres", "overview", "streaming_services" },
            additionalProperties = false
        }
    };
}
