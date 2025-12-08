using System.ComponentModel.DataAnnotations;

namespace LoveLetter.App.Data;

public class BucketListEntry
{
    public Guid Id { get; set; }

    [MaxLength(160)]
    public required string Title { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    public bool RequiresPhoto { get; set; }

    public bool Completed { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public List<BucketListMedia> Media { get; set; } = [];
}
