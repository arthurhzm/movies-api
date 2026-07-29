using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MoviesAPI.Models;
using MoviesAPI.Services;

namespace MoviesAPI.Tests;

public sealed class LetterboxdServiceTests
{
    [Fact]
    public async Task ImportRatingsCsvAsync_ReimportIsIdempotentAndChangedRatingIsUpdated()
    {
        await using var context = TestSupport.CreateContext();
        context.Auth.Add(new AuthModel { Id = 1, Username = "alice" });
        await context.SaveChangesAsync();
        var service = TestSupport.CreateService(context);
        const string initialCsv = "Date,Name,Year,Letterboxd URI,Rating\n"
            + "2020-05-10,Arrival,2016,https://boxd.it/arrival,4.0\n";

        await using (var firstStream = TestSupport.CsvStream(initialCsv))
        {
            var first = await service.ImportRatingsCsvAsync(1, firstStream);
            Assert.Equal(1, first.Created);
            Assert.Equal(0, first.Updated);
            Assert.Equal(0, first.Unchanged);
        }

        var feedback = await context.UserMovieFeedback.SingleAsync();
        var originalCreatedAt = feedback.CreatedAt;
        feedback.Review = "Minha resenha local";
        await context.SaveChangesAsync();

        await using (var identicalStream = TestSupport.CsvStream(initialCsv))
        {
            var identical = await service.ImportRatingsCsvAsync(1, identicalStream);
            Assert.Equal(0, identical.Created);
            Assert.Equal(0, identical.Updated);
            Assert.Equal(1, identical.Unchanged);
            Assert.Equal(1, identical.TotalMovies);
        }

        const string changedCsv = "Date,Name,Year,Letterboxd URI,Rating\n"
            + "2025-02-01,Arrival,2016,https://boxd.it/arrival,4.5\n";
        await using (var changedStream = TestSupport.CsvStream(changedCsv))
        {
            var changed = await service.ImportRatingsCsvAsync(1, changedStream);
            Assert.Equal(0, changed.Created);
            Assert.Equal(1, changed.Updated);
            Assert.Equal(0, changed.Unchanged);
        }

        feedback = await context.UserMovieFeedback.SingleAsync();
        Assert.Equal(4.5, feedback.Rating);
        Assert.Equal("Minha resenha local", feedback.Review);
        Assert.Equal(originalCreatedAt, feedback.CreatedAt);
        Assert.NotNull((await context.Auth.FindAsync(1))!.LetterboxdLastImport);
    }

    [Fact]
    public async Task ImportRatingsCsvAsync_InvalidLaterRowPersistsNothing()
    {
        await using var context = TestSupport.CreateContext();
        context.Auth.Add(new AuthModel { Id = 1, Username = "alice" });
        await context.SaveChangesAsync();
        var service = TestSupport.CreateService(context);
        const string csv = "Date,Name,Year,Letterboxd URI,Rating\n"
            + "2025-01-01,Arrival,2016,https://boxd.it/arrival,4.5\n"
            + "2025-01-02,Dune,2021,https://boxd.it/dune,4.25\n";

        await using var stream = TestSupport.CsvStream(csv);
        await Assert.ThrowsAsync<LetterboxdCsvValidationException>(
            () => service.ImportRatingsCsvAsync(1, stream));

        Assert.Empty(await context.UserMovieFeedback.ToListAsync());
        Assert.Null((await context.Auth.FindAsync(1))!.LetterboxdLastImport);
    }

    [Fact]
    public async Task ImportRatingsCsvAsync_SameTitleDifferentYearsCreatesDistinctMovies()
    {
        await using var context = TestSupport.CreateContext();
        context.Auth.Add(new AuthModel { Id = 1, Username = "alice" });
        await context.SaveChangesAsync();
        var service = TestSupport.CreateService(context);
        const string csv = "Date,Name,Year,Letterboxd URI,Rating\n"
            + "2025-01-01,Dune,1984,https://boxd.it/dune-1984,3.0\n"
            + "2025-01-02,Dune,2021,https://boxd.it/dune-2021,4.5\n";

        await using var stream = TestSupport.CsvStream(csv);
        var result = await service.ImportRatingsCsvAsync(1, stream);

        Assert.Equal(2, result.Created);
        var movies = await context.UserMovieFeedback.OrderBy(movie => movie.MovieYear).ToListAsync();
        Assert.Collection(
            movies,
            movie => Assert.Equal(1984, movie.MovieYear),
            movie => Assert.Equal(2021, movie.MovieYear));
    }

    [Fact]
    public async Task ImportRatingsCsvAsync_LegacyTitleMatchCorrectsScaleAndPreservesLocalFields()
    {
        await using var context = TestSupport.CreateContext();
        var createdAt = new DateTime(2023, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        context.Auth.Add(new AuthModel { Id = 1, Username = "alice" });
        context.UserMovieFeedback.Add(new UserMovieFeedbackModel
        {
            UserId = 1,
            MovieTitle = "  the   matrix ",
            Rating = 9,
            Review = "Não remover",
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        });
        await context.SaveChangesAsync();
        var service = TestSupport.CreateService(context);
        const string csv = "Date,Name,Year,Letterboxd URI,Rating\n"
            + "2025-01-01,The Matrix,1999,https://boxd.it/matrix,4.5\n";

        await using var stream = TestSupport.CsvStream(csv);
        var result = await service.ImportRatingsCsvAsync(1, stream);

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Updated);
        var feedback = await context.UserMovieFeedback.SingleAsync();
        Assert.Equal("The Matrix", feedback.MovieTitle);
        Assert.Equal(1999, feedback.MovieYear);
        Assert.Equal("https://boxd.it/matrix", feedback.LetterboxdUri);
        Assert.Equal(4.5, feedback.Rating);
        Assert.Equal("Não remover", feedback.Review);
        Assert.Equal(createdAt, feedback.CreatedAt);
    }

    [Fact]
    public async Task SyncUserAsync_RssRatingRemainsOnFivePointScale()
    {
        await using var context = TestSupport.CreateContext();
        context.Auth.Add(new AuthModel
        {
            Id = 1,
            Username = "alice",
            LetterboxdUsername = "alice_lb"
        });
        await context.SaveChangesAsync();
        const string rss = """
            <rss xmlns:letterboxd="https://letterboxd.com">
              <channel>
                <item>
                  <letterboxd:filmTitle>Arrival</letterboxd:filmTitle>
                  <letterboxd:filmYear>2016</letterboxd:filmYear>
                  <letterboxd:memberRating>4.5</letterboxd:memberRating>
                  <link>https://letterboxd.com/alice_lb/film/arrival/</link>
                  <pubDate>Mon, 01 Jan 2024 10:00:00 +0000</pubDate>
                </item>
                <item>
                  <letterboxd:filmTitle>Arrival</letterboxd:filmTitle>
                  <letterboxd:filmYear>2016</letterboxd:filmYear>
                  <letterboxd:memberRating>2.0</letterboxd:memberRating>
                  <link>https://letterboxd.com/alice_lb/film/arrival/</link>
                  <pubDate>Sun, 01 Jan 2023 10:00:00 +0000</pubDate>
                </item>
              </channel>
            </rss>
            """;
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(rss, Encoding.UTF8, "application/rss+xml")
        };
        var service = TestSupport.CreateService(context, response);

        var result = await service.SyncUserAsync(1);

        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Updated);
        var feedback = await context.UserMovieFeedback.SingleAsync();
        Assert.Equal(4.5, feedback.Rating);
        Assert.Equal(2016, feedback.MovieYear);
        Assert.Equal("https://letterboxd.com/film/arrival/", feedback.LetterboxdUri);
        Assert.NotNull((await context.Auth.FindAsync(1))!.LetterboxdLastSync);
    }

    [Fact]
    public async Task ImportRatingsCsvAsync_WhenHistoryChanges_InvalidatesGeneratedRecommendations()
    {
        await using var context = TestSupport.CreateContext();
        context.Auth.Add(new AuthModel { Id = 1, Username = "alice" });
        context.GeneratedRecommendations.Add(new GeneratedRecommendationModel
        {
            UserId = 1,
            MovieTitle = "Cached recommendation",
            GeneratedAt = DateTime.UtcNow,
            IsSpecial = false
        });
        await context.SaveChangesAsync();
        var service = TestSupport.CreateService(context);
        const string csv = "Date,Name,Year,Letterboxd URI,Rating\n"
            + "2025-01-01,Arrival,2016,https://boxd.it/arrival,4.5\n";

        await using var stream = TestSupport.CsvStream(csv);
        await service.ImportRatingsCsvAsync(1, stream);

        Assert.Empty(await context.GeneratedRecommendations.ToListAsync());
    }
}
