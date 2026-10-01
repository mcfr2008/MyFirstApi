using Microsoft.AspNetCore.Mvc.Filters;

namespace MyFirstApi.Idempotency;

// Marks a POST action as safe to retry with an Idempotency-Key header (see IdempotencyFilter).
[AttributeUsage(AttributeTargets.Method)]
public class IdempotentAttribute : Attribute, IFilterFactory
{
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        ActivatorUtilities.CreateInstance<IdempotencyFilter>(serviceProvider);
}
