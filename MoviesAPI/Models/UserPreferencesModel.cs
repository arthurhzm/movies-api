namespace MoviesAPI.Models;

public class UserPreferencesModel
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public List<string> FavoriteGenres { get; set; } = new List<string>();
    public List<string> FavoriteDirectors { get; set; } = new List<string>();
    public List<string> FavoriteActors { get; set; } = new List<string>();
    public int? MinReleaseYear { get; set; }
    public int? MaxDuration { get; set; }
    public bool AcceptAdultContent { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}