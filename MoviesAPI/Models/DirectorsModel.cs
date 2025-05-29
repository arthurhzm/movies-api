namespace MoviesAPI.Models;

public class DirectorsModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public float PopularityScore { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

}