using System.Globalization;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using MoviesAPI.Models;

namespace MoviesAPI.Services;

public sealed record LetterboxdRatingRecord(
    int RowNumber,
    DateTime RatedAt,
    string MovieTitle,
    int MovieYear,
    string LetterboxdUri,
    double Rating);

public sealed record LetterboxdCsvParseResult(
    IReadOnlyList<LetterboxdRatingRecord> Ratings,
    int RowsRead,
    int Duplicates);

public sealed record LetterboxdCsvError(int Row, string Field, string Message);

public sealed class LetterboxdCsvValidationException : Exception
{
    public LetterboxdCsvValidationException(string message, IReadOnlyList<LetterboxdCsvError> errors)
        : base(message)
    {
        Errors = errors;
    }

    public IReadOnlyList<LetterboxdCsvError> Errors { get; }
}

public sealed class LetterboxdCsvParser
{
    public const int MaxRows = 50_000;
    private const int MaxErrors = 100;
    private static readonly string[] RequiredHeaders = ["Date", "Name", "Year", "Letterboxd URI", "Rating"];

    public LetterboxdCsvParseResult Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var errors = new List<LetterboxdCsvError>();
        var ratingsByUri = new Dictionary<string, LetterboxdRatingRecord>(StringComparer.Ordinal);
        var rowsRead = 0;
        var duplicates = 0;
        var recordNumber = 1;

        try
        {
            using var parser = new TextFieldParser(stream, new UTF8Encoding(false, true), true)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = false
            };
            parser.SetDelimiters(",");

            var headers = parser.ReadFields();
            if (headers is null)
            {
                throw InvalidFile("O arquivo CSV está vazio.", new LetterboxdCsvError(1, "Header", "Cabeçalho ausente."));
            }

            var headerIndexes = BuildHeaderIndexes(headers, errors);
            if (errors.Count > 0)
            {
                throw InvalidFile("O cabeçalho do ratings.csv é inválido.", errors);
            }

            while (!parser.EndOfData)
            {
                recordNumber++;
                string[]? fields;
                try
                {
                    fields = parser.ReadFields();
                }
                catch (MalformedLineException)
                {
                    AddError(errors, recordNumber, "CSV", "Linha CSV malformada.");
                    continue;
                }

                if (fields is null || fields.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                rowsRead++;
                if (rowsRead > MaxRows)
                {
                    throw InvalidFile(
                        $"O arquivo excede o limite de {MaxRows} avaliações.",
                        new LetterboxdCsvError(recordNumber, "CSV", "Quantidade máxima de avaliações excedida."));
                }

                var rating = ParseRecord(fields, headerIndexes, recordNumber, errors);
                if (rating is null)
                {
                    continue;
                }

                if (ratingsByUri.ContainsKey(rating.LetterboxdUri))
                {
                    duplicates++;
                }

                // Em uma exportação com duplicata, a última linha é a versão mais recente.
                ratingsByUri[rating.LetterboxdUri] = rating;
            }
        }
        catch (DecoderFallbackException)
        {
            throw InvalidFile(
                "O arquivo precisa estar codificado em UTF-8.",
                new LetterboxdCsvError(recordNumber, "CSV", "Codificação inválida; use UTF-8."));
        }

        if (errors.Count > 0)
        {
            throw InvalidFile("O ratings.csv contém dados inválidos. Nenhuma avaliação foi importada.", errors);
        }

        if (rowsRead == 0 || ratingsByUri.Count == 0)
        {
            throw InvalidFile(
                "O ratings.csv não contém avaliações.",
                new LetterboxdCsvError(2, "CSV", "Nenhuma avaliação encontrada."));
        }

        return new LetterboxdCsvParseResult(ratingsByUri.Values.ToList(), rowsRead, duplicates);
    }

    public static bool TryNormalizeLetterboxdUri(string value, out string normalizedUri)
    {
        normalizedUri = string.Empty;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !uri.IsDefaultPort)
        {
            return false;
        }

        var host = uri.IdnHost.TrimEnd('.');
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (string.Equals(host, "boxd.it", StringComparison.OrdinalIgnoreCase))
        {
            if (segments.Length != 1)
            {
                return false;
            }

            normalizedUri = $"https://boxd.it/{segments[0]}";
            return true;
        }

        if (!string.Equals(host, "letterboxd.com", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(host, "www.letterboxd.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var filmSegment = Array.FindIndex(segments, segment => string.Equals(segment, "film", StringComparison.OrdinalIgnoreCase));
        if (filmSegment < 0 || filmSegment + 1 >= segments.Length)
        {
            return false;
        }

        normalizedUri = $"https://letterboxd.com/film/{segments[filmSegment + 1]}/";
        return true;
    }

    private static Dictionary<string, int> BuildHeaderIndexes(string[] headers, List<LetterboxdCsvError> errors)
    {
        var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < headers.Length; index++)
        {
            var header = headers[index].Trim().TrimStart('\uFEFF');
            if (string.IsNullOrEmpty(header))
            {
                continue;
            }

            if (!indexes.TryAdd(header, index))
            {
                AddError(errors, 1, header, "Coluna duplicada no cabeçalho.");
            }
        }

        foreach (var requiredHeader in RequiredHeaders)
        {
            if (!indexes.ContainsKey(requiredHeader))
            {
                AddError(errors, 1, requiredHeader, "Coluna obrigatória ausente.");
            }
        }

        return indexes;
    }

    private static LetterboxdRatingRecord? ParseRecord(
        string[] fields,
        IReadOnlyDictionary<string, int> indexes,
        int rowNumber,
        List<LetterboxdCsvError> errors)
    {
        var errorCountBefore = errors.Count;
        var dateValue = GetField(fields, indexes["Date"]);
        var nameValue = GetField(fields, indexes["Name"]).Trim();
        var yearValue = GetField(fields, indexes["Year"]).Trim();
        var uriValue = GetField(fields, indexes["Letterboxd URI"]).Trim();
        var ratingValue = GetField(fields, indexes["Rating"]).Trim();

        DateTime ratedAt = default;
        if (!DateOnly.TryParseExact(dateValue.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ratedDate))
        {
            AddError(errors, rowNumber, "Date", "Data inválida; use yyyy-MM-dd.");
        }
        else
        {
            ratedAt = DateTime.SpecifyKind(ratedDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        }

        if (string.IsNullOrWhiteSpace(nameValue))
        {
            AddError(errors, rowNumber, "Name", "Nome do filme ausente.");
        }
        else if (nameValue.Length > 500)
        {
            AddError(errors, rowNumber, "Name", "Nome do filme excede 500 caracteres.");
        }

        var movieYear = 0;
        if (!int.TryParse(yearValue, NumberStyles.None, CultureInfo.InvariantCulture, out movieYear)
            || movieYear is < 1800 or > 2100)
        {
            AddError(errors, rowNumber, "Year", "Ano inválido.");
        }

        if (!TryNormalizeLetterboxdUri(uriValue, out var normalizedUri))
        {
            AddError(errors, rowNumber, "Letterboxd URI", "URI do Letterboxd inválida.");
        }
        else if (normalizedUri.Length > UserMovieFeedbackModel.MaxLetterboxdUriLength)
        {
            AddError(
                errors,
                rowNumber,
                "Letterboxd URI",
                $"URI do Letterboxd excede {UserMovieFeedbackModel.MaxLetterboxdUriLength} caracteres.");
        }

        decimal parsedRating = 0;
        if (!decimal.TryParse(ratingValue, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out parsedRating)
            || parsedRating is < 0.5m or > 5.0m
            || parsedRating * 2 != decimal.Truncate(parsedRating * 2))
        {
            AddError(errors, rowNumber, "Rating", "Nota inválida; use valores de 0.5 a 5.0 em passos de 0.5.");
        }

        if (errors.Count != errorCountBefore)
        {
            return null;
        }

        return new LetterboxdRatingRecord(
            rowNumber,
            ratedAt,
            nameValue.Normalize(NormalizationForm.FormC),
            movieYear,
            normalizedUri,
            decimal.ToDouble(parsedRating));
    }

    private static string GetField(string[] fields, int index) => index < fields.Length ? fields[index] : string.Empty;

    private static void AddError(List<LetterboxdCsvError> errors, int row, string field, string message)
    {
        if (errors.Count < MaxErrors)
        {
            errors.Add(new LetterboxdCsvError(row, field, message));
        }
    }

    private static LetterboxdCsvValidationException InvalidFile(string message, LetterboxdCsvError error) =>
        new(message, [error]);

    private static LetterboxdCsvValidationException InvalidFile(string message, IReadOnlyList<LetterboxdCsvError> errors) =>
        new(message, errors);
}
