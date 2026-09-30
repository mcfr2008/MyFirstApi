using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

public class LocationService
    : MasterDataService<Location, LocationRequest, LocationResponse, LocationQuery>, ILocationService
{
    public LocationService(AppDbContext context) : base(context)
    {
    }

    protected override DbSet<Location> Set => Context.Locations;

    protected override string EntityName => "Location";

    protected override Expression<Func<Location, LocationResponse>> Projection => l => new LocationResponse
    {
        Id = l.Id,
        Code = l.Code,
        Name = l.Name,
        Type = l.Type,
        AddressLine = l.AddressLine,
        SubDistrict = l.SubDistrict,
        District = l.District,
        Province = l.Province,
        PostalCode = l.PostalCode,
        Country = l.Country,
        UnLocode = l.UnLocode,
        IataCode = l.IataCode,
        TimeZone = l.TimeZone,
        Latitude = l.Latitude,
        Longitude = l.Longitude,
        ContactName = l.ContactName,
        ContactPhone = l.ContactPhone,
        IsActive = l.IsActive,
        CreatedAt = l.CreatedAt,
        UpdatedAt = l.UpdatedAt
    };

    protected override Expression<Func<Location, bool>> MatchesSearch(string pattern) =>
        l => EF.Functions.ILike(l.Code, pattern) ||
             EF.Functions.ILike(l.Name, pattern) ||
             (l.UnLocode != null && EF.Functions.ILike(l.UnLocode, pattern)) ||
             (l.IataCode != null && EF.Functions.ILike(l.IataCode, pattern));

    protected override IQueryable<Location> ApplyFilters(IQueryable<Location> items, LocationQuery query)
    {
        if (query.Type.HasValue)
        {
            items = items.Where(l => l.Type == query.Type.Value);
        }
        if (!string.IsNullOrWhiteSpace(query.Province))
        {
            var province = query.Province.Trim().ToLower();
            items = items.Where(l => l.Province != null && l.Province.ToLower() == province);
        }
        return items;
    }

    protected override void Apply(Location entity, LocationRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.Type = request.Type!.Value;
        entity.AddressLine = QueryHelpers.NullIfBlank(request.AddressLine);
        entity.SubDistrict = QueryHelpers.NullIfBlank(request.SubDistrict);
        entity.District = QueryHelpers.NullIfBlank(request.District);
        entity.Province = QueryHelpers.NullIfBlank(request.Province);
        entity.PostalCode = QueryHelpers.NullIfBlank(request.PostalCode);
        entity.Country = QueryHelpers.NullIfBlank(request.Country)?.ToUpperInvariant() ?? "TH";
        entity.UnLocode = QueryHelpers.NullIfBlank(request.UnLocode)?.ToUpperInvariant();
        entity.IataCode = QueryHelpers.NullIfBlank(request.IataCode)?.ToUpperInvariant();
        entity.TimeZone = QueryHelpers.NullIfBlank(request.TimeZone) ?? "Asia/Bangkok";
        entity.Latitude = request.Latitude;
        entity.Longitude = request.Longitude;
        entity.ContactName = QueryHelpers.NullIfBlank(request.ContactName);
        entity.ContactPhone = QueryHelpers.NullIfBlank(request.ContactPhone);
    }
}
