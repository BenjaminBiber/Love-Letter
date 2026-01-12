using LoveLetter.App.Data;
using LoveLetter.App.Models;
using LoveLetter.App.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace LoveLetter.App.Endpoints;

public static class TravelEndpoints
{
    public static RouteGroupBuilder MapTravelEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/travel");
        group.MapGet("/", GetAsync);
        group.MapPost("/", AddAsync);
        group.MapPost("/{id:guid}/visited", SetVisitedAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        return group;
    }

    private static async Task<Ok<List<TravelCountryDto>>> GetAsync(LoveLetterDbContext db)
    {
        var items = await db.TravelCountries
            .AsNoTracking()
            .OrderBy(c => c.IsVisited ? 0 : 1)
            .ThenByDescending(c => c.VisitedAt ?? c.CreatedAt)
            .ThenBy(c => c.CountryName)
            .Select(c => c.ToDto())
            .ToListAsync();

        return TypedResults.Ok(items);
    }

    private static async Task<Results<BadRequest<string>, Ok<TravelCountryDto>>> AddAsync(
        CreateTravelCountryRequest request,
        ITravelDestinationService travelService)
    {
        var result = await travelService.AddPlannedAsync(request.Code);
        return result.Success
            ? TypedResults.Ok(result.Value!)
            : TypedResults.BadRequest(result.Error ?? "Land konnte nicht hinzugefügt werden.");
    }

    private static async Task<Results<BadRequest<string>, Ok<TravelCountryDto>>> SetVisitedAsync(
        Guid id,
        bool visited,
        ITravelDestinationService travelService)
    {
        var result = await travelService.SetVisitedAsync(id, visited);
        return result.Success
            ? TypedResults.Ok(result.Value!)
            : TypedResults.BadRequest(result.Error ?? "Konnte Status nicht setzen.");
    }

    private static async Task<Results<BadRequest<string>, Ok<bool>>> DeleteAsync(Guid id, ITravelDestinationService travelService)
    {
        var result = await travelService.RemoveAsync(id);
        return result.Success
            ? TypedResults.Ok(true)
            : TypedResults.BadRequest(result.Error ?? "Konnte Eintrag nicht löschen.");
    }
}
