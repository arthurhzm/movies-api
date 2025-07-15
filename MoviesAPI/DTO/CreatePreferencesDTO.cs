namespace MoviesAPI.DTO;

public class SetUserPreferencesDTO
{
    public List<string> Genres { get; set; } = new List<string>();
    public List<string> Directors { get; set; } = new List<string>();
    public List<string> Actors { get; set; } = new List<string>();
    public int? MinReleaseYear { get; set; }
    public int? MaxDuration { get; set; }
    public bool AcceptAdultContent { get; set; }
}