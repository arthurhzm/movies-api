using System.Text.Json.Serialization;

namespace MoviesAPI.DTO;

public sealed record AiSearchRequest(string? Query);

public sealed record AiChatMessageRequest(string? Text, string? Sender);

public sealed record AiChatRequest(IReadOnlyList<AiChatMessageRequest>? Messages);

public sealed class AiMovieSearchResultDTO
{
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public List<string> Genres { get; set; } = new();
    public string Overview { get; set; } = string.Empty;

    [JsonPropertyName("streaming_services")]
    public List<string> StreamingServices { get; set; } = new();
}
