using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Services;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // JwtBearer maps the token's "sub" claim to NameIdentifier by default.
    public string? Username
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        }
    }
}
