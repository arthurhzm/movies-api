namespace MoviesAPI.DTO;

public sealed record LetterboxdImportResultDTO(
    int RowsRead,
    int Created,
    int Updated,
    int Unchanged,
    int Duplicates,
    int TotalMovies,
    DateTime ImportedAt)
{
    public int Imported => Created + Updated;
}

public sealed record LetterboxdSyncResultDTO(int Created, int Updated)
{
    public int Imported => Created;
}

public sealed record LetterboxdStatusCountsDTO(int LetterboxdMovies, int TotalMovies);
