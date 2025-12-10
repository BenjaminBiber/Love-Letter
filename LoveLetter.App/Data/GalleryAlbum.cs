using System.ComponentModel.DataAnnotations;

namespace LoveLetter.App.Data;

public class GalleryAlbum
{
    public Guid Id { get; set; }

    [MaxLength(80)]
    public required string Name { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
