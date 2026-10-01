using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Exceptions;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

public class ServiceAreaService
    : MasterDataService<ServiceArea, ServiceAreaRequest, ServiceAreaResponse, ServiceAreaQuery>, IServiceAreaService
{
    private static readonly LocationType[] StationTypes =
        [LocationType.Branch, LocationType.DropPoint, LocationType.Hub, LocationType.Warehouse];

    private static readonly LocationType[] HubTypes = [LocationType.Hub];

    public ServiceAreaService(AppDbContext context) : base(context)
    {
    }

    protected override DbSet<ServiceArea> Set => Context.ServiceAreas;

    protected override string EntityName => "ServiceArea";

    protected override Expression<Func<ServiceArea, ServiceAreaResponse>> Projection => a => new ServiceAreaResponse
    {
        Id = a.Id,
        Code = a.Code,
        Name = a.Name,
        Country = a.Country,
        Province = a.Province,
        PostalCode = a.PostalCode,
        Station = new ReferenceSummary(a.StationLocation.Id, a.StationLocation.Code, a.StationLocation.Name),
        Hub = new ReferenceSummary(a.HubLocation.Id, a.HubLocation.Code, a.HubLocation.Name),
        IsActive = a.IsActive,
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt
    };

    protected override Expression<Func<ServiceArea, bool>> MatchesSearch(string pattern) =>
        a => EF.Functions.ILike(a.Code, pattern) ||
             EF.Functions.ILike(a.Name, pattern) ||
             (a.Province != null && EF.Functions.ILike(a.Province, pattern)) ||
             (a.PostalCode != null && EF.Functions.ILike(a.PostalCode, pattern));

    protected override IQueryable<ServiceArea> ApplyFilters(IQueryable<ServiceArea> items, ServiceAreaQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Country))
        {
            var country = query.Country.Trim().ToUpperInvariant();
            items = items.Where(a => a.Country == country);
        }
        if (!string.IsNullOrWhiteSpace(query.Province))
        {
            var province = query.Province.Trim().ToLower();
            items = items.Where(a => a.Province != null && a.Province.ToLower() == province);
        }
        if (!string.IsNullOrWhiteSpace(query.PostalCode))
        {
            var postalCode = NormalizePostalCode(query.PostalCode);
            items = items.Where(a => a.PostalCode == postalCode);
        }
        if (query.StationLocationId.HasValue)
        {
            items = items.Where(a => a.StationLocationId == query.StationLocationId);
        }
        if (query.HubLocationId.HasValue)
        {
            items = items.Where(a => a.HubLocationId == query.HubLocationId);
        }
        return items;
    }

    protected override IOrderedQueryable<ServiceArea> ApplyOrder(IQueryable<ServiceArea> items) =>
        items.OrderBy(a => a.Country).ThenBy(a => a.Province).ThenBy(a => a.PostalCode);

    protected override async Task ValidateAsync(ServiceArea entity, ServiceAreaRequest request)
    {
        var currentStationId = entity.Id == 0 ? (int?)null : entity.StationLocationId;
        var currentHubId = entity.Id == 0 ? (int?)null : entity.HubLocationId;

        var station = await ReferenceResolver.ResolveRequiredAsync(
            Context.Locations.AsNoTracking(), request.StationLocationId!.Value, currentStationId, "stationLocationId");
        EnsureType(station, StationTypes, "stationLocationId");

        var hub = await ReferenceResolver.ResolveRequiredAsync(
            Context.Locations.AsNoTracking(), request.HubLocationId!.Value, currentHubId, "hubLocationId");
        EnsureType(hub, HubTypes, "hubLocationId");

        // One area per postal code, and one province-wide area per province.
        var country = request.Country.Trim().ToUpperInvariant();
        var postalCode = NormalizePostalCode(request.PostalCode);
        var province = QueryHelpers.NullIfBlank(request.Province);
        var overlaps = Set.AsNoTracking().Where(a => a.Id != entity.Id && a.Country == country);
        overlaps = postalCode != null
            ? overlaps.Where(a => a.PostalCode == postalCode)
            : overlaps.Where(a => a.PostalCode == null && a.Province!.ToLower() == province!.ToLower());

        var existing = await overlaps.Select(a => a.Code).FirstOrDefaultAsync();
        if (existing != null)
        {
            throw Errors.ServiceAreaOverlap(Coverage(country, postalCode, postalCode == null ? province : null), existing);
        }
    }

    protected override void Apply(ServiceArea entity, ServiceAreaRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.Country = request.Country.Trim().ToUpperInvariant();
        entity.Province = QueryHelpers.NullIfBlank(request.Province);
        entity.PostalCode = NormalizePostalCode(request.PostalCode);
        entity.StationLocationId = request.StationLocationId!.Value;
        entity.HubLocationId = request.HubLocationId!.Value;
    }

    public async Task<ServiceAreaResolveResponse> ResolveAsync(ServiceAreaResolveQuery query)
    {
        var country = query.Country.Trim().ToUpperInvariant();
        var postalCode = NormalizePostalCode(query.PostalCode);
        var province = QueryHelpers.NullIfBlank(query.Province);

        if (query.LocationId.HasValue)
        {
            var location = await Context.Locations.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == query.LocationId.Value)
                ?? throw Errors.ReferenceNotFound("locationId", query.LocationId.Value);
            country = location.Country;
            postalCode = NormalizePostalCode(location.PostalCode);
            province = QueryHelpers.NullIfBlank(location.Province);
        }

        return await FindAsync(country, postalCode, province)
               ?? throw Errors.ServiceAreaNotCovered(Coverage(country, postalCode, province));
    }

    public async Task<ServiceAreaResolveResponse?> FindAsync(string country, string? postalCode, string? province)
    {
        country = country.Trim().ToUpperInvariant();
        postalCode = NormalizePostalCode(postalCode);
        province = QueryHelpers.NullIfBlank(province);

        var active = Set.AsNoTracking().Where(a => a.IsActive && a.Country == country);
        if (postalCode != null)
        {
            var byPostalCode = await active.Where(a => a.PostalCode == postalCode)
                .Select(Projection).FirstOrDefaultAsync();
            if (byPostalCode != null)
            {
                return new ServiceAreaResolveResponse { MatchedBy = ServiceAreaMatch.PostalCode, ServiceArea = byPostalCode };
            }
        }
        if (province != null)
        {
            var lowered = province.ToLower();
            var byProvince = await active.Where(a => a.PostalCode == null && a.Province!.ToLower() == lowered)
                .Select(Projection).FirstOrDefaultAsync();
            if (byProvince != null)
            {
                return new ServiceAreaResolveResponse { MatchedBy = ServiceAreaMatch.Province, ServiceArea = byProvince };
            }
        }
        return null;
    }

    private static void EnsureType(Location location, LocationType[] allowed, string field)
    {
        if (!allowed.Contains(location.Type))
        {
            throw Errors.LocationTypeNotAllowed(field, location.Code, location.Type, allowed.Cast<object>());
        }
    }

    private static string? NormalizePostalCode(string? postalCode) =>
        QueryHelpers.NullIfBlank(postalCode)?.ToUpperInvariant();

    // e.g. "TH 50200 เชียงใหม่" for error messages.
    public static string Coverage(string country, string? postalCode, string? province) =>
        string.Join(" ", new[] { country, postalCode, province }.Where(p => p != null));
}
