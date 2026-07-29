using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;
using MoviesAPI.DTO;
using MoviesAPI.Models;

namespace MoviesAPI.Services;

public sealed class LetterboxdSyncException : Exception
{
    public LetterboxdSyncException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public class LetterboxdService
{
    private static readonly Regex ValidUsername = new("^[A-Za-z0-9_]{2,15}$", RegexOptions.Compiled);
    private static readonly Regex CollapsedWhitespace = new("\\s+", RegexOptions.Compiled);

    private readonly AppDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly ILogger<LetterboxdService> _logger;
    private readonly LetterboxdCsvParser _csvParser;

    public LetterboxdService(
        AppDbContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<LetterboxdService> logger,
        LetterboxdCsvParser csvParser)
    {
        _context = context;
        _httpClient = httpClientFactory.CreateClient("LetterboxdClient");
        _logger = logger;
        _csvParser = csvParser;
    }

    public static bool IsValidUsername(string username) => ValidUsername.IsMatch(username);

    public async Task<LetterboxdSyncResultDTO> SyncUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Auth.FindAsync([userId], cancellationToken);
        if (user == null || string.IsNullOrWhiteSpace(user.LetterboxdUsername))
        {
            return new LetterboxdSyncResultDTO(0, 0);
        }

        var username = user.LetterboxdUsername.Trim();
        if (!IsValidUsername(username))
        {
            throw new LetterboxdSyncException("O username conectado ao Letterboxd é inválido.");
        }

        var feedUrl = $"https://letterboxd.com/{Uri.EscapeDataString(username)}/rss/";
        string xmlContent;

        try
        {
            using var response = await _httpClient.GetAsync(feedUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new LetterboxdSyncException($"O Letterboxd respondeu com status {(int)response.StatusCode}.");
            }

            xmlContent = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (LetterboxdSyncException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Failed to fetch Letterboxd RSS for user {UserId} ({Username})", userId, username);
            throw new LetterboxdSyncException("Não foi possível consultar o RSS do Letterboxd.", ex);
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(xmlContent);
        }
        catch (XmlException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Letterboxd RSS XML for user {UserId}", userId);
            throw new LetterboxdSyncException("O Letterboxd retornou um RSS inválido.", ex);
        }

        XNamespace letterboxd = "https://letterboxd.com";
        var ratingsByIdentity = new Dictionary<
            string,
            (LetterboxdRatingRecord Rating, DateTimeOffset? PublishedAt)>(StringComparer.Ordinal);

        foreach (var item in document.Descendants("item"))
        {
            var movieTitle = item.Element(letterboxd + "filmTitle")?.Value.Trim();
            var yearValue = item.Element(letterboxd + "filmYear")?.Value;
            var ratingValue = item.Element(letterboxd + "memberRating")?.Value;
            var linkValue = item.Element("link")?.Value;

            if (string.IsNullOrWhiteSpace(movieTitle)
                || !int.TryParse(yearValue, NumberStyles.None, CultureInfo.InvariantCulture, out var movieYear)
                || !TryParseRating(ratingValue, out var rating)
                || string.IsNullOrWhiteSpace(linkValue)
                || !LetterboxdCsvParser.TryNormalizeLetterboxdUri(linkValue, out var normalizedUri))
            {
                continue;
            }

            DateTimeOffset? publishedAt = null;
            if (DateTimeOffset.TryParse(
                item.Element("pubDate")?.Value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsedPublishedAt))
            {
                publishedAt = parsedPublishedAt;
            }

            var candidate = new LetterboxdRatingRecord(
                0,
                publishedAt?.UtcDateTime ?? DateTime.UtcNow,
                movieTitle.Normalize(NormalizationForm.FormC),
                movieYear,
                normalizedUri,
                rating);

            if (!ratingsByIdentity.TryGetValue(normalizedUri, out var existing)
                || (publishedAt.HasValue
                    && (!existing.PublishedAt.HasValue || publishedAt > existing.PublishedAt)))
            {
                ratingsByIdentity[normalizedUri] = (candidate, publishedAt);
            }
        }

        var now = DateTime.UtcNow;
        var ratings = ratingsByIdentity.Values.Select(entry => entry.Rating).ToList();
        var outcome = await MergeRatingsAsync(user, ratings, now, cancellationToken);
        user.LetterboxdLastSync = now;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Letterboxd RSS sync for user {UserId}: created {Created}, updated {Updated}",
            userId,
            outcome.Created,
            outcome.Updated);

        return new LetterboxdSyncResultDTO(outcome.Created, outcome.Updated);
    }

    public async Task<LetterboxdImportResultDTO> ImportRatingsCsvAsync(
        int userId,
        Stream csvStream,
        CancellationToken cancellationToken = default)
    {
        // O arquivo inteiro é validado antes de qualquer alteração rastreada pelo EF.
        var parsed = _csvParser.Parse(csvStream);
        var user = await _context.Auth.FindAsync([userId], cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var importedAt = DateTime.UtcNow;
        var outcome = await MergeRatingsAsync(user, parsed.Ratings, importedAt, cancellationToken);
        user.LetterboxdLastImport = importedAt;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Letterboxd CSV import for user {UserId}: rows {RowsRead}, created {Created}, updated {Updated}, unchanged {Unchanged}",
            userId,
            parsed.RowsRead,
            outcome.Created,
            outcome.Updated,
            outcome.Unchanged);

        return new LetterboxdImportResultDTO(
            parsed.RowsRead,
            outcome.Created,
            outcome.Updated,
            outcome.Unchanged,
            parsed.Duplicates,
            outcome.TotalMovies,
            importedAt);
    }

    public async Task<LetterboxdStatusCountsDTO> GetStatusCountsAsync(int userId, CancellationToken cancellationToken = default)
    {
        var totalMovies = await _context.UserMovieFeedback
            .CountAsync(feedback => feedback.UserId == userId, cancellationToken);
        var letterboxdMovies = await _context.UserMovieFeedback
            .CountAsync(feedback => feedback.UserId == userId && feedback.LetterboxdUri != null, cancellationToken);

        return new LetterboxdStatusCountsDTO(letterboxdMovies, totalMovies);
    }

    private async Task<MergeOutcome> MergeRatingsAsync(
        AuthModel user,
        IReadOnlyCollection<LetterboxdRatingRecord> ratings,
        DateTime updatedAt,
        CancellationToken cancellationToken)
    {
        var existingFeedback = await _context.UserMovieFeedback
            .Where(feedback => feedback.UserId == user.Id)
            .ToListAsync(cancellationToken);

        var byUri = existingFeedback
            .Where(feedback => !string.IsNullOrWhiteSpace(feedback.LetterboxdUri))
            .GroupBy(feedback => feedback.LetterboxdUri!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var byTitleAndYear = existingFeedback
            .Where(feedback => feedback.MovieYear.HasValue)
            .GroupBy(feedback => BuildTitleKey(feedback.MovieTitle, feedback.MovieYear))
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var legacyByTitle = existingFeedback
            .Where(feedback => !feedback.MovieYear.HasValue && string.IsNullOrWhiteSpace(feedback.LetterboxdUri))
            .GroupBy(feedback => NormalizeTitle(feedback.MovieTitle))
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var claimedIds = new HashSet<int>();
        var created = 0;
        var updated = 0;
        var unchanged = 0;

        foreach (var rating in ratings)
        {
            var feedback = FindUniqueUnclaimed(byUri.GetValueOrDefault(rating.LetterboxdUri), claimedIds)
                ?? FindUniqueUnclaimed(byTitleAndYear.GetValueOrDefault(BuildTitleKey(rating.MovieTitle, rating.MovieYear)), claimedIds)
                ?? FindUniqueUnclaimed(legacyByTitle.GetValueOrDefault(NormalizeTitle(rating.MovieTitle)), claimedIds);

            if (feedback is null)
            {
                _context.UserMovieFeedback.Add(new UserMovieFeedbackModel
                {
                    UserId = user.Id,
                    MovieTitle = rating.MovieTitle,
                    MovieYear = rating.MovieYear,
                    LetterboxdUri = rating.LetterboxdUri,
                    Rating = rating.Rating,
                    CreatedAt = rating.RatedAt,
                    UpdatedAt = updatedAt
                });
                created++;
                continue;
            }

            claimedIds.Add(feedback.Id);
            var changed = false;

            if (!string.Equals(feedback.MovieTitle, rating.MovieTitle, StringComparison.Ordinal))
            {
                feedback.MovieTitle = rating.MovieTitle;
                changed = true;
            }

            if (feedback.MovieYear != rating.MovieYear)
            {
                feedback.MovieYear = rating.MovieYear;
                changed = true;
            }

            if (!string.Equals(feedback.LetterboxdUri, rating.LetterboxdUri, StringComparison.Ordinal))
            {
                feedback.LetterboxdUri = rating.LetterboxdUri;
                changed = true;
            }

            if (Math.Abs(feedback.Rating - rating.Rating) > 0.0001)
            {
                // O CSV é a fonte autoritativa para corrigir também imports RSS antigos em escala 1–10.
                feedback.Rating = rating.Rating;
                changed = true;
            }

            if (changed)
            {
                feedback.UpdatedAt = updatedAt;
                updated++;
            }
            else
            {
                unchanged++;
            }
        }

        if (created > 0 || updated > 0)
        {
            var cachedRecommendations = await _context.GeneratedRecommendations
                .Where(recommendation => recommendation.UserId == user.Id)
                .ToListAsync(cancellationToken);
            _context.GeneratedRecommendations.RemoveRange(cachedRecommendations);
        }

        return new MergeOutcome(created, updated, unchanged, existingFeedback.Count + created);
    }

    private static UserMovieFeedbackModel? FindUniqueUnclaimed(
        IReadOnlyCollection<UserMovieFeedbackModel>? candidates,
        IReadOnlySet<int> claimedIds)
    {
        if (candidates is null)
        {
            return null;
        }

        UserMovieFeedbackModel? match = null;
        foreach (var candidate in candidates)
        {
            if (claimedIds.Contains(candidate.Id))
            {
                continue;
            }

            if (match is not null)
            {
                return null;
            }

            match = candidate;
        }

        return match;
    }

    private static bool TryParseRating(string? value, out double rating)
    {
        rating = 0;
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed)
            || parsed is < 0.5m or > 5.0m
            || parsed * 2 != decimal.Truncate(parsed * 2))
        {
            return false;
        }

        rating = decimal.ToDouble(parsed);
        return true;
    }

    private static string BuildTitleKey(string title, int? year) =>
        $"{year?.ToString(CultureInfo.InvariantCulture) ?? "-"}\u001f{NormalizeTitle(title)}";

    private static string NormalizeTitle(string title) =>
        CollapsedWhitespace.Replace(title.Normalize(NormalizationForm.FormC).Trim(), " ").ToUpperInvariant();

    private sealed record MergeOutcome(int Created, int Updated, int Unchanged, int TotalMovies);
}
