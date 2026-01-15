using LoveLetter.App.Models;
using LoveLetter.App.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LoveLetter.App.Endpoints;

public static class WatchlistEndpoints
{
    public static RouteGroupBuilder MapWatchlistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/watchlist");
        group.MapGet("/", GetAsync);
        group.MapGet("/search", SearchAsync);
        group.MapPost("/", AddAsync);
        group.MapPost("/{id:guid}/watched", SetWatchedAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        return group;
    }

    private static async Task<Ok<IReadOnlyList<WatchlistMovieDto>>> GetAsync(IWatchlistService watchlistService)
    {
        var result = await watchlistService.GetAsync();
        return TypedResults.Ok(result.Value ?? Array.Empty<WatchlistMovieDto>());
    }

    private static async Task<Results<BadRequest<string>, Ok<IReadOnlyList<WatchlistSearchResult>>>> SearchAsync(
        string query,
        IWatchlistService watchlistService)
    {
        var result = await watchlistService.SearchAsync(query);
        return result.Success
            ? TypedResults.Ok(result.Value ?? Array.Empty<WatchlistSearchResult>())
            : TypedResults.BadRequest(result.Error ?? "Suche fehlgeschlagen.");
    }

    private static async Task<Results<BadRequest<string>, Ok<WatchlistMovieDto>>> AddAsync(
        AddWatchlistMovieRequest request,
        IWatchlistService watchlistService)
    {
        var result = await watchlistService.AddAsync(request.ImdbId);
        return result.Success
            ? TypedResults.Ok(result.Value!)
            : TypedResults.BadRequest(result.Error ?? "Film konnte nicht hinzugefügt werden.");
    }

    private static async Task<Results<BadRequest<string>, Ok<WatchlistMovieDto>>> SetWatchedAsync(
        Guid id,
        SetWatchedRequest request,
        IWatchlistService watchlistService)
    {
        var result = await watchlistService.SetWatchedAsync(id, request.Watched);
        return result.Success
            ? TypedResults.Ok(result.Value!)
            : TypedResults.BadRequest(result.Error ?? "Status konnte nicht aktualisiert werden.");
    }

    private static async Task<Results<BadRequest<string>, Ok>> DeleteAsync(
        Guid id,
        IWatchlistService watchlistService)
    {
        var result = await watchlistService.DeleteAsync(id);
        return result.Success
            ? TypedResults.Ok()
            : TypedResults.BadRequest(result.Error ?? "Film konnte nicht gelöscht werden.");
    }
}
