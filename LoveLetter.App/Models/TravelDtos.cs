using System.ComponentModel.DataAnnotations;
using LoveLetter.App.Data;

namespace LoveLetter.App.Models;

public sealed record TravelCountryDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public bool IsVisited { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? VisitedAt { get; init; }
}

public sealed record CreateTravelCountryRequest
{
    [Required]
    [StringLength(3, MinimumLength = 2)]
    public string Code { get; init; } = string.Empty;
}

public static class TravelMappings
{
    public static TravelCountryDto ToDto(this TravelCountry country) => new TravelCountryDto
    {
        Id = country.Id,
        Code = country.CountryCode,
        Name = country.CountryName,
        IsVisited = country.IsVisited,
        CreatedAt = country.CreatedAt,
        VisitedAt = country.VisitedAt
    };
}
