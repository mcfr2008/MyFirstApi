using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MyFirstApi.Authorization;
using MyFirstApi.Data;
using MyFirstApi.Exceptions;
using MyFirstApi.Interfaces;
using MyFirstApi.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. เพิ่ม Controllers แทน Minimal API
builder.Services.AddControllers();

// 2. Swagger / OpenAPI with JWT Support
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a JWT token obtained from POST /api/Auth/login."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document, null)] = new List<string>()
    });
});

// 3. ตั้งค่า Database Connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString,
        // Needed to map Dictionary properties (TrackedItem.Attributes) to jsonb.
        npgsql => npgsql.ConfigureDataSource(dataSource => dataSource.EnableDynamicJson())));

// 4. ลงทะเบียน Dependency Injection (DI) สำหรับ Service Pattern
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITrackedItemService, TrackedItemService>();
builder.Services.AddScoped<IItemCategoryService, ItemCategoryService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IPartyService, PartyService>();
builder.Services.AddScoped<IEventTypeService, EventTypeService>();
builder.Services.AddScoped<ICarrierService, CarrierService>();
builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<IContainerService, ContainerService>();
builder.Services.AddScoped<IReasonCodeService, ReasonCodeService>();
builder.Services.AddScoped<ITrackingEventRecorder, TrackingEventRecorder>();
builder.Services.AddScoped<ITrackingEventService, TrackingEventService>();
builder.Services.AddScoped<IShipmentService, ShipmentService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IMasterDataCache, MasterDataCache>();
builder.Services.AddHostedService<PartitionMaintenanceService>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Turns ConflictException / BusinessRuleException from services into 409 / 400.
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// Every error response is ProblemDetails (RFC 9457) with a stable "code" the
// frontend can translate (see Exceptions/Errors.cs and GET /api/v1/ErrorCodes).
// ApiExceptionHandler sets code/args for business errors; this fills in the
// code for errors produced by the framework (validation, 401, 404, ...).
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var extensions = context.ProblemDetails.Extensions;
        if (extensions.ContainsKey("code")) return;

        extensions["code"] = context.ProblemDetails switch
        {
            Microsoft.AspNetCore.Mvc.ValidationProblemDetails => Errors.ValidationFailed.Code,
            { Status: StatusCodes.Status401Unauthorized } => Errors.Unauthorized.Code,
            { Status: StatusCodes.Status403Forbidden } => Errors.Forbidden.Code,
            { Status: StatusCodes.Status404NotFound } => Errors.NotFound.Code,
            { Status: >= 500 } => Errors.InternalError.Code,
            { Status: var status } => $"HTTP_{status}"
        };
    };
});

// URL-segment versioning: /api/v1/... Controllers without [ApiVersion] are v1.
// Breaking changes go into a new version so existing apps keep working.
builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        // Swagger document "v1"; the {version} route segment is filled in.
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

// Browsers only let the frontend call the API from origins listed in
// Cors:AllowedOrigins (appsettings.Development.json has common dev servers).
const string FrontendCorsPolicy = "Frontend";
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders("Location", "api-supported-versions"));
});

// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!))
        };
    });

// Database-driven permissions: [Authorize(Policy = "...")] names are resolved
// at request time against the Permissions/RolePermissions tables instead of
// being registered as fixed policies here.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Secure by default: every endpoint requires authentication unless it opts out
// with [AllowAnonymous] (e.g. AuthController.Login).
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

app.UseExceptionHandler();
// Gives empty error responses (e.g. 401 from JWT, unknown routes) a ProblemDetails body.
app.UseStatusCodePages();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

// 5. แมป Routing ไปหา Controllers
app.MapControllers();

app.Run();
