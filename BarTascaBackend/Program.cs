using System.Text;
using BarTasca.Data;
using BarTasca.Infrastructure;
using BarTasca.Services;
using BarTasca.Services.Mapping;
using BarTasca.Services.Options;
using BarTascaBackend;
using BarTascaBackend.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;


try
{
    DotNetEnv.Env.Load();
}
catch
{
    throw new InvalidOperationException("Error loading environment variables from .env file");
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.ListenAnyIP(8080));

static string EnvOrThrow(string key) =>
    Environment.GetEnvironmentVariable(key)
    ?? throw new InvalidOperationException($"{key} not set");

// Jwt config
var jwt = new JwtOptions
{
    Secret = EnvOrThrow("JWT_SECRET"),
    Issuer = EnvOrThrow("JWT_ISSUER"),
    Audience = EnvOrThrow("JWT_AUDIENCE"),
    ExpiresHours = 24
};

builder.Services.AddSingleton<IOptions<JwtOptions>>(Options.Create(jwt));
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret));


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

// DbContext
var connectionString =
    $"Server={EnvOrThrow("MYSQL_HOST")};" +
    $"Port={EnvOrThrow("MYSQL_PORT")};" +
    $"Database={EnvOrThrow("MYSQL_DB")};" +
    $"Uid={EnvOrThrow("MYSQL_USER")};" +
    $"Pwd={EnvOrThrow("MYSQL_PASSWORD")};";

builder.Services.AddDbContext<ColaDbContext>(opt =>
    opt.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Repos y servicios de aplicación
builder.Services.AddRepositories();
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<TicketMappingProfile>());
builder.Services.AddApplicationServices();

// SignalR + Infrastructure
builder.Services.AddApiLayer();
builder.Services.AddSignalR();
builder.Services.AddInfrastructure<QueueHub>();


//Workers
builder.Services.AddBackgroundWorkers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Sirve archivos estáticos desde wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<QueueHub>("/hubs/queue");

app.Run();
