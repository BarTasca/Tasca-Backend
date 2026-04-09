using System.Text;
using BarTasca.Data;
using BarTasca.Infrastructure;
using BarTasca.Services;
using BarTasca.Services.Interfaces;
using BarTasca.Services.Mapping;
using BarTasca.Services.Options;
using BarTascaBackend;
using BarTascaBackend.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    try
    {
        DotNetEnv.Env.Load();
    }
    catch
    {
    }
}

// CORS
var prodCorsOrigins = EnvListOrEmpty("CORS_ALLOWED_ORIGINS");

if (!builder.Environment.IsDevelopment() && prodCorsOrigins.Length == 0)
{
    throw new InvalidOperationException("CORS_ALLOWED_ORIGINS not set");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevFront", p =>
        p.WithOrigins(
            "http://localhost:5173",
            "http://localhost:5174",
            "http://192.168.1.133:5173"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());

    options.AddPolicy("ProdFront", p =>
        p.WithOrigins(prodCorsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// Helpers
static string EnvOrThrow(string key) =>
    Environment.GetEnvironmentVariable(key)
    ?? throw new InvalidOperationException($"{key} not set");

static string? EnvOrNull(string key) =>
    Environment.GetEnvironmentVariable(key);

static string[] EnvListOrEmpty(string key) =>
    (Environment.GetEnvironmentVariable(key) ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

// Jwt config
var jwt = new JwtOptions
{
    Secret = EnvOrThrow("JWT_SECRET"),
    Issuer = EnvOrThrow("JWT_ISSUER"),
    Audience = EnvOrThrow("JWT_AUDIENCE")
};

builder.Services.AddSingleton<IOptions<JwtOptions>>(Options.Create(jwt));
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret));

// WebPush config
var webPush = new WebPushOptions
{
    VapidPublicKey = EnvOrNull("WEBPUSH_VAPID_PUBLIC_KEY"),
    VapidPrivateKey = EnvOrNull("WEBPUSH_VAPID_PRIVATE_KEY"),
    Subject = EnvOrNull("WEBPUSH_SUBJECT"),
    PublicAppBaseUrl = EnvOrNull("PUBLIC_APP_BASE_URL"),
};

builder.Services.AddSingleton<IOptions<WebPushOptions>>(Options.Create(webPush));

if (!builder.Environment.IsDevelopment() &&
    !string.IsNullOrWhiteSpace(webPush.VapidPublicKey) &&
    !string.IsNullOrWhiteSpace(webPush.VapidPrivateKey) &&
    !string.IsNullOrWhiteSpace(webPush.Subject) &&
    string.IsNullOrWhiteSpace(webPush.PublicAppBaseUrl))
{
    throw new InvalidOperationException("PUBLIC_APP_BASE_URL not set while WebPush is enabled");
}

// Autenticación JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/queue"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },

            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";

                var result = System.Text.Json.JsonSerializer.Serialize(new
                {
                    message = "Unauthorized: token is missing or invalid."
                });

                return context.Response.WriteAsync(result);
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Staff", policy => policy.RequireRole("Admin", "Worker"));
});

// DbContext
var connectionString =
    $"Server={EnvOrThrow("MYSQL_HOST")};" +
    $"Port={EnvOrThrow("MYSQL_PORT")};" +
    $"Database={EnvOrThrow("MYSQL_DB")};" +
    $"Uid={EnvOrThrow("MYSQL_USER")};" +
    $"Pwd={EnvOrThrow("MYSQL_PASSWORD")};";

builder.Services.AddDbContext<ColaDbContext>(opt =>
    opt.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36))));

// Repos y servicios de aplicación
builder.Services.AddRepositories();
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<TicketMappingProfile>());
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<ServiceStateMappingProfile>());
builder.Services.AddApplicationServices();

// Options
builder.Services.Configure<QrOptions>(builder.Configuration.GetSection("Qr"));

// SignalR + Infrastructure
builder.Services.AddApiLayer();
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});
builder.Services.AddInfrastructure<QueueHub>();
builder.Services.AddPublicInfrastructure<PublicQueueHub>();

// Workers
builder.Services.AddBackgroundWorkers();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseCors(app.Environment.IsDevelopment() ? "DevFront" : "ProdFront");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<QueueHub>("/hubs/queue");
app.MapHub<PublicQueueHub>("/hubs/public-queue");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ColaDbContext>();
    db.Database.Migrate();

    var initialAdminService = scope.ServiceProvider.GetRequiredService<IInitialAdminService>();
    await initialAdminService.EnsureInitialAdminAsync();
}

app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();