using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

public class CarrierService
    : MasterDataService<Carrier, CarrierRequest, CarrierResponse, CarrierQuery>, ICarrierService
{
    public CarrierService(AppDbContext context) : base(context)
    {
    }

    protected override DbSet<Carrier> Set => Context.Carriers;

    protected override string EntityName => "Carrier";

    protected override Expression<Func<Carrier, CarrierResponse>> Projection => c => new CarrierResponse
    {
        Id = c.Id,
        Code = c.Code,
        Name = c.Name,
        ModeNames = c.Modes,
        ScacCode = c.ScacCode,
        IataCode = c.IataCode,
        ContactName = c.ContactName,
        Phone = c.Phone,
        Email = c.Email,
        Website = c.Website,
        TrackingUrlTemplate = c.TrackingUrlTemplate,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };

    protected override Expression<Func<Carrier, bool>> MatchesSearch(string pattern) =>
        c => EF.Functions.ILike(c.Code, pattern) ||
             EF.Functions.ILike(c.Name, pattern) ||
             (c.ScacCode != null && EF.Functions.ILike(c.ScacCode, pattern)) ||
             (c.IataCode != null && EF.Functions.ILike(c.IataCode, pattern));

    protected override IQueryable<Carrier> ApplyFilters(IQueryable<Carrier> items, CarrierQuery query)
    {
        if (!query.Mode.HasValue) return items;

        var mode = query.Mode.Value.ToString();
        return items.Where(c => c.Modes.Contains(mode));
    }

    protected override IOrderedQueryable<Carrier> ApplyOrder(IQueryable<Carrier> items) =>
        items.OrderBy(c => c.Name);

    protected override void Apply(Carrier entity, CarrierRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.Modes = request.Modes.Distinct().Select(m => m.ToString()).ToList();
        entity.ScacCode = QueryHelpers.NullIfBlank(request.ScacCode)?.ToUpperInvariant();
        entity.IataCode = QueryHelpers.NullIfBlank(request.IataCode)?.ToUpperInvariant();
        entity.ContactName = QueryHelpers.NullIfBlank(request.ContactName);
        entity.Phone = QueryHelpers.NullIfBlank(request.Phone);
        entity.Email = QueryHelpers.NullIfBlank(request.Email);
        entity.Website = QueryHelpers.NullIfBlank(request.Website);
        entity.TrackingUrlTemplate = QueryHelpers.NullIfBlank(request.TrackingUrlTemplate);
    }
}
