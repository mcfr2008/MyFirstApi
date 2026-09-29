using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Exceptions;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

public class ReasonCodeService
    : MasterDataService<ReasonCode, ReasonCodeRequest, ReasonCodeResponse, ReasonCodeQuery>, IReasonCodeService
{
    public ReasonCodeService(AppDbContext context) : base(context)
    {
    }

    protected override DbSet<ReasonCode> Set => Context.ReasonCodes;

    protected override string EntityName => "Reason code";

    protected override Expression<Func<ReasonCode, ReasonCodeResponse>> Projection => r => new ReasonCodeResponse
    {
        Id = r.Id,
        Code = r.Code,
        NameTh = r.NameTh,
        NameEn = r.NameEn,
        Description = r.Description,
        EventTypeCodes = r.EventTypeCodes,
        RequiresNote = r.RequiresNote,
        SortOrder = r.SortOrder,
        IsActive = r.IsActive,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt
    };

    protected override Expression<Func<ReasonCode, bool>> MatchesSearch(string pattern) =>
        r => EF.Functions.ILike(r.Code, pattern) ||
             EF.Functions.ILike(r.NameTh, pattern) ||
             EF.Functions.ILike(r.NameEn, pattern);

    protected override IQueryable<ReasonCode> ApplyFilters(IQueryable<ReasonCode> items, ReasonCodeQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.EventTypeCode)) return items;

        var eventTypeCode = QueryHelpers.NormalizeCode(query.EventTypeCode);
        return items.Where(r => r.EventTypeCodes.Count == 0 || r.EventTypeCodes.Contains(eventTypeCode));
    }

    protected override IOrderedQueryable<ReasonCode> ApplyOrder(IQueryable<ReasonCode> items) =>
        items.OrderBy(r => r.SortOrder).ThenBy(r => r.Code);

    protected override async Task ValidateAsync(ReasonCode entity, ReasonCodeRequest request)
    {
        var codes = NormalizeEventTypeCodes(request.EventTypeCodes);
        if (codes.Count == 0) return;

        var known = await Context.EventTypes.Where(e => codes.Contains(e.Code)).Select(e => e.Code).ToListAsync();
        var unknown = codes.Except(known).ToList();
        if (unknown.Count > 0)
        {
            throw new BusinessRuleException($"Unknown event type codes: {string.Join(", ", unknown)}");
        }
    }

    protected override void Apply(ReasonCode entity, ReasonCodeRequest request)
    {
        entity.NameTh = request.NameTh.Trim();
        entity.NameEn = request.NameEn.Trim();
        entity.Description = QueryHelpers.NullIfBlank(request.Description);
        entity.EventTypeCodes = NormalizeEventTypeCodes(request.EventTypeCodes);
        entity.RequiresNote = request.RequiresNote;
        entity.SortOrder = request.SortOrder;
    }

    private static List<string> NormalizeEventTypeCodes(List<string>? codes) =>
        (codes ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(QueryHelpers.NormalizeCode)
            .Distinct()
            .ToList();
}
