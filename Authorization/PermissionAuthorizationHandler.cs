using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using System.Security.Claims;

namespace MyFirstApi.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly AppDbContext _context;

    public PermissionAuthorizationHandler(AppDbContext context)
    {
        _context = context;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var roles = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (roles.Count == 0)
        {
            return;
        }

        var hasPermission = await _context.RolePermissions
            .AnyAsync(rp => roles.Contains(rp.Role) && rp.Permission.Code == requirement.Permission);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
