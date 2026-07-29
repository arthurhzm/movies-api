using System.Text;
using MoviesAPI.Services;

namespace MoviesAPI.Tests;

public sealed class LetterboxdCsvParserTests
{
    private readonly LetterboxdCsvParser _parser = new();

    [Fact]
    public void Parse_BomQuotedTitleAndReorderedHeaders_ReturnsNormalizedRatings()
    {
        const string csv = "\uFEFFRating,Name,Letterboxd URI,Date,Year\r\n"
            + "4.5,\"Once Upon a Time, in Hollywood\",https://boxd.it/abc,2025-01-02,2019\r\n"
            + "3.0,Amélie,https://www.letterboxd.com/member/film/amelie/?source=export,2025-01-03,2001\r\n";

        using var stream = TestSupport.CsvStream(csv);
        var result = _parser.Parse(stream);

        Assert.Equal(2, result.RowsRead);
        Assert.Equal(0, result.Duplicates);
        Assert.Collection(
            result.Ratings,
            first =>
            {
                Assert.Equal("Once Upon a Time, in Hollywood", first.MovieTitle);
                Assert.Equal(2019, first.MovieYear);
                Assert.Equal("https://boxd.it/abc", first.LetterboxdUri);
                Assert.Equal(4.5, first.Rating);
                Assert.Equal(DateTimeKind.Utc, first.RatedAt.Kind);
            },
            second =>
            {
                Assert.Equal("Amélie", second.MovieTitle);
                Assert.Equal("https://letterboxd.com/film/amelie/", second.LetterboxdUri);
                Assert.Equal(3.0, second.Rating);
            });
    }

    [Fact]
    public void Parse_DuplicateUri_KeepsLastRowAndReportsDuplicate()
    {
        const string csv = "Date,Name,Year,Letterboxd URI,Rating\n"
            + "2024-01-01,Arrival,2016,https://boxd.it/arrival,4.0\n"
            + "2025-01-01,Arrival,2016,https://boxd.it/arrival,4.5\n";

        using var stream = TestSupport.CsvStream(csv);
        var result = _parser.Parse(stream);

        var rating = Assert.Single(result.Ratings);
        Assert.Equal(2, result.RowsRead);
        Assert.Equal(1, result.Duplicates);
        Assert.Equal(4.5, rating.Rating);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), rating.RatedAt);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("5.5")]
    [InlineData("4.25")]
    [InlineData("not-a-rating")]
    public void Parse_InvalidRating_RejectsWholeFile(string rating)
    {
        var csv = "Date,Name,Year,Letterboxd URI,Rating\n"
            + $"2025-01-01,Arrival,2016,https://boxd.it/arrival,{rating}\n";

        using var stream = TestSupport.CsvStream(csv);
        var exception = Assert.Throws<LetterboxdCsvValidationException>(() => _parser.Parse(stream));

        var error = Assert.Single(exception.Errors);
        Assert.Equal(2, error.Row);
        Assert.Equal("Rating", error.Field);
    }

    [Fact]
    public void Parse_MissingRequiredHeader_ReportsHeaderError()
    {
        const string csv = "Date,Name,Year,Letterboxd URI\n"
            + "2025-01-01,Arrival,2016,https://boxd.it/arrival\n";

        using var stream = TestSupport.CsvStream(csv);
        var exception = Assert.Throws<LetterboxdCsvValidationException>(() => _parser.Parse(stream));

        Assert.Contains(exception.Errors, error => error.Row == 1 && error.Field == "Rating");
    }

    [Fact]
    public void Parse_NonUtf8File_ReportsEncodingError()
    {
        var invalidUtf8 = Encoding.Latin1.GetBytes(
            "Date,Name,Year,Letterboxd URI,Rating\n2025-01-01,Amélie,2001,https://boxd.it/amelie,4.5\n");
        using var stream = new MemoryStream(invalidUtf8);

        var exception = Assert.Throws<LetterboxdCsvValidationException>(() => _parser.Parse(stream));

        Assert.Contains(exception.Errors, error => error.Message.Contains("UTF-8", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_UriLongerThanPersistenceLimit_RejectsWholeFile()
    {
        var slug = new string('a', 300);
        var csv = "Date,Name,Year,Letterboxd URI,Rating\n"
            + $"2025-01-01,Arrival,2016,https://letterboxd.com/film/{slug}/,4.5\n";

        using var stream = TestSupport.CsvStream(csv);
        var exception = Assert.Throws<LetterboxdCsvValidationException>(() => _parser.Parse(stream));

        Assert.Contains(
            exception.Errors,
            error => error.Field == "Letterboxd URI" && error.Message.Contains("256", StringComparison.Ordinal));
    }
}
