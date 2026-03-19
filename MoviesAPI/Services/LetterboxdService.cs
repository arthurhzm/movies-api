using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using MoviesAPI.Data;
using MoviesAPI.Models;

namespace MoviesAPI.Services;

public class LetterboxdService
{
    private readonly AppDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly ILogger<LetterboxdService> _logger;

    public LetterboxdService(AppDbContext context, IHttpClientFactory httpClientFactory, ILogger<LetterboxdService> logger)
    {
        _context = context;
        _httpClient = httpClientFactory.CreateClient("LetterboxdClient");
        _logger = logger;
    }

    public async Task<int> SyncUserAsync(int userId)
    {
        var user = await _context.Auth.FindAsync(userId);
        if (user == null || string.IsNullOrEmpty(user.LetterboxdUsername))
            return 0;

        var feedUrl = $"https://letterboxd.com/{user.LetterboxdUsername}/rss/";

        string xmlContent;
        try
        {
            xmlContent = await _httpClient.GetStringAsync(feedUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Letterboxd RSS for user {UserId} ({Username})", userId, user.LetterboxdUsername);
            return 0;
        }

        XNamespace lb = "https://letterboxd.com";
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xmlContent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Letterboxd RSS XML for user {UserId}", userId);
            return 0;
        }

        var items = doc.Descendants("item").ToList();
        int imported = 0;

        foreach (var item in items)
        {
            var filmTitle = item.Element(lb + "filmTitle")?.Value;
            if (string.IsNullOrEmpty(filmTitle)) continue;

            // Check for duplicates
            var exists = await _context.UserMovieFeedback
                .AnyAsync(f => f.UserId == userId && f.MovieTitle == filmTitle);
            if (exists) continue;

            double rating = 0;
            var ratingElement = item.Element(lb + "memberRating");
            if (ratingElement != null && double.TryParse(ratingElement.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var letterboxdRating))
            {
                // Letterboxd: 0.5–5.0 → CineMatch: 1.0–10.0
                rating = Math.Round(letterboxdRating * 2, 1);
            }

            _context.UserMovieFeedback.Add(new UserMovieFeedbackModel
            {
                UserId = userId,
                MovieTitle = filmTitle,
                Rating = rating,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            imported++;
        }

        if (imported > 0)
        {
            await _context.SaveChangesAsync();
        }

        user.LetterboxdLastSync = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Letterboxd sync for user {UserId}: imported {Count} movies", userId, imported);
        return imported;
    }

    public async Task<int> GetImportedCountAsync(int userId)
    {
        // Return total watched movies (all came from letterboxd or manual)
        // We track all feedback count as a proxy
        return await _context.UserMovieFeedback
            .CountAsync(f => f.UserId == userId);
    }
}
