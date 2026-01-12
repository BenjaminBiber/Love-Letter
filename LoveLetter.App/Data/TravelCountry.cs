using System.ComponentModel.DataAnnotations;

namespace LoveLetter.App.Data;

public class TravelCountry
{
    public Guid Id { get; set; }

    [MaxLength(3)]
    public required string CountryCode { get; set; }

    [MaxLength(120)]
    public required string CountryName { get; set; }

    public bool IsVisited { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? VisitedAt { get; set; }
}
