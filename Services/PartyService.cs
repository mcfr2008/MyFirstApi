using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

public class PartyService
    : MasterDataService<Party, PartyRequest, PartyResponse, PartyQuery>, IPartyService
{
    public PartyService(AppDbContext context) : base(context)
    {
    }

    protected override DbSet<Party> Set => Context.Parties;

    protected override string EntityName => "Party";

    protected override Expression<Func<Party, PartyResponse>> Projection => p => new PartyResponse
    {
        Id = p.Id,
        Code = p.Code,
        Name = p.Name,
        Type = p.Type,
        TaxId = p.TaxId,
        ContactName = p.ContactName,
        Phone = p.Phone,
        Email = p.Email,
        AddressLine = p.AddressLine,
        SubDistrict = p.SubDistrict,
        District = p.District,
        Province = p.Province,
        PostalCode = p.PostalCode,
        Country = p.Country,
        IsActive = p.IsActive,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };

    protected override Expression<Func<Party, bool>> MatchesSearch(string pattern) =>
        p => EF.Functions.ILike(p.Code, pattern) ||
             EF.Functions.ILike(p.Name, pattern) ||
             (p.Phone != null && EF.Functions.ILike(p.Phone, pattern)) ||
             (p.Email != null && EF.Functions.ILike(p.Email, pattern));

    protected override IQueryable<Party> ApplyFilters(IQueryable<Party> items, PartyQuery query) =>
        query.Type.HasValue ? items.Where(p => p.Type == query.Type.Value) : items;

    protected override void Apply(Party entity, PartyRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.Type = request.Type!.Value;
        entity.TaxId = QueryHelpers.NullIfBlank(request.TaxId);
        entity.ContactName = QueryHelpers.NullIfBlank(request.ContactName);
        entity.Phone = QueryHelpers.NullIfBlank(request.Phone);
        entity.Email = QueryHelpers.NullIfBlank(request.Email);
        entity.AddressLine = QueryHelpers.NullIfBlank(request.AddressLine);
        entity.SubDistrict = QueryHelpers.NullIfBlank(request.SubDistrict);
        entity.District = QueryHelpers.NullIfBlank(request.District);
        entity.Province = QueryHelpers.NullIfBlank(request.Province);
        entity.PostalCode = QueryHelpers.NullIfBlank(request.PostalCode);
        entity.Country = QueryHelpers.NullIfBlank(request.Country)?.ToUpperInvariant() ?? "TH";
    }
}
