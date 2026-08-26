namespace Collectify.Domain.Entities;

public class Genre
{
    public int Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public ICollection<Movie> Movies { get; set; } = new List<Movie>();
    public ICollection<MusicAlbum> MusicAlbums { get; set; } = new List<MusicAlbum>();
    public ICollection<Game> Games { get; set; } = new List<Game>();
}
