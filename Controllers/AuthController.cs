using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Authorization;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var token = await _authService.LoginAsync(request.Username, request.Password);
            if (token == null)
            {
                return Unauthorized(new { message = "Invalid username or password" });
            }

            return Ok(new { token });
        }

        // Who can create accounts and assign roles is controlled by the
        // RolePermissions table (Auth.Register), not hardcoded here.
        // The first admin account is seeded directly in Scripts/create_tables.sql.
        [Authorize(Policy = Permissions.AuthRegister)]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var created = await _authService.RegisterAsync(request.Username, request.Password, request.Role);
            if (!created)
            {
                return Conflict(new { message = "Username already exists" });
            }

            return Ok(new { message = "User created" });
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = Roles.User;
    }
}
