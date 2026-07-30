using System.Text.Json;

namespace MoviesAPI.Services;

/// <summary>
/// Resolves a movie (by title + optional year) to its canonical TMDB id, which
/// CineMatch uses as the stable movie identity across imports and manual ratings.
/// </summary>
public interface ITmdbResolver
{
    Task<int?> ResolveTmdbIdAsync(string title, int? year, CancellationToken cancellationToken = default);
}

/// <summary>Fallback used when no TMDB token is configured (and in tests): resolves nothing.</summary>
public sealed class NoopTmdbResolver : ITmdbResolver
{
    public Task<int?> ResolveTmdbIdAsync(string title, int? year, CancellationToken cancellationToken = default)
        => Task.FromResult<int?>(null);
}

public sealed class TmdbResolver : ITmdbResolver
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TmdbResolver> _logger;

    public TmdbResolver(IHttpClientFactory httpClientFactory, ILogger<TmdbResolver> logger)
    {
        _httpClient = httpClientFactory.CreateClient("TmdbClient");
        _logger = logger;
    }

    public async Task<int?> ResolveTmdbIdAsync(string title, int? year, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        try
        {
            // No year filter in the query (Letterboxd years occasionally differ); rank by year instead.
            var url = $"search/movie?query={Uri.EscapeDataString(title)}&language=pt-BR&include_adult=true";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TMDB search returned {Status} for '{Title}'", (int)response.StatusCode, title);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("results", out var results)
                || results.ValueKind != JsonValueKind.Array
                || results.GetArrayLength() == 0)
            {
                return null;
            }

            int? firstId = null;
            foreach (var result in results.EnumerateArray())
            {
                if (!result.TryGetProperty("id", out var idElement) || !idElement.TryGetInt32(out var id))
                {
                    continue;
                }

                firstId ??= id;

                // Prefer the candidate whose release year matches the Letterboxd year.
                if (year is > 0
                    && result.TryGetProperty("release_date", out var releaseDate)
                    && releaseDate.ValueKind == JsonValueKind.String)
                {
                    var releaseText = releaseDate.GetString();
                    if (!string.IsNullOrEmpty(releaseText)
                        && releaseText.Length >= 4
                        && int.TryParse(releaseText[..4], out var releaseYear)
                        && releaseYear == year)
                    {
                        return id;
                    }
                }
            }

            // No exact-year match: fall back to TMDB's most relevant result.
            return firstId;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "TMDB resolve failed for '{Title}'", title);
            return null;
        }
    }
}
