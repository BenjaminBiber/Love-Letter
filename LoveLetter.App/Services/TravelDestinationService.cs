using LoveLetter.App.Data;
using LoveLetter.App.Models;
using Microsoft.EntityFrameworkCore;

namespace LoveLetter.App.Services;

public interface ITravelDestinationService
{
    Task<IReadOnlyList<TravelCountryDto>> GetAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<TravelCountryDto>> AddPlannedAsync(string code, CancellationToken cancellationToken = default);
    Task<ServiceResult<TravelCountryDto>> SetVisitedAsync(Guid id, bool isVisited, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> RemoveAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class TravelDestinationService : ITravelDestinationService
{
    private readonly IDbContextFactory<LoveLetterDbContext> _dbContextFactory;
    private readonly ICountryCatalogService _countryCatalog;

    public TravelDestinationService(IDbContextFactory<LoveLetterDbContext> dbContextFactory, ICountryCatalogService countryCatalog)
    {
        _dbContextFactory = dbContextFactory;
        _countryCatalog = countryCatalog;
    }

    public async Task<IReadOnlyList<TravelCountryDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.TravelCountries
            .AsNoTracking()
            .OrderBy(c => c.IsVisited ? 0 : 1)
            .ThenByDescending(c => c.VisitedAt ?? c.CreatedAt)
            .ThenBy(c => c.CountryName)
            .ToListAsync(cancellationToken);

        return items.Select(c => c.ToDto()).ToList();
    }

    public async Task<ServiceResult<TravelCountryDto>> AddPlannedAsync(string code, CancellationToken cancellationToken = default)
    {
        var country = await _countryCatalog.FindByCodeAsync(code, cancellationToken);
        if (country is null)
        {
            return ServiceResult<TravelCountryDto>.Fail("Land konnte nicht gefunden werden.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.TravelCountries.FirstOrDefaultAsync(c => c.CountryCode == country.Code, cancellationToken);
        if (existing is not null)
        {
            existing.CountryName = country.Name;
            await db.SaveChangesAsync(cancellationToken);
            return ServiceResult<TravelCountryDto>.Ok(existing.ToDto());
        }

        var entry = new TravelCountry
        {
            Id = Guid.NewGuid(),
            CountryCode = country.Code,
            CountryName = country.Name,
            IsVisited = false,
            CreatedAt = DateTime.UtcNow
        };

        db.TravelCountries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<TravelCountryDto>.Ok(entry.ToDto());
    }

    public async Task<ServiceResult<TravelCountryDto>> SetVisitedAsync(Guid id, bool isVisited, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = await db.TravelCountries.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entry is null)
        {
            return ServiceResult<TravelCountryDto>.Fail("Land wurde nicht gefunden.");
        }

        entry.IsVisited = isVisited;
        entry.VisitedAt = isVisited ? DateTime.UtcNow : null;
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<TravelCountryDto>.Ok(entry.ToDto());
    }

    public async Task<ServiceResult<bool>> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = await db.TravelCountries.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entry is null)
        {
            return ServiceResult<bool>.Fail("Eintrag existiert nicht mehr.");
        }

        db.TravelCountries.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<bool>.Ok(true);
    }
}
