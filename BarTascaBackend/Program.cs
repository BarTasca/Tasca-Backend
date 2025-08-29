using BarTasca.Data;
using BarTasca.Data.Interfaces;
using BarTasca.Infrastructure;
using BarTasca.Services;
using BarTasca.Services.Mapping;
using BarTasca.Services.Services;
using BarTasca.Services.Interfaces;
using BarTascaBackend;
using BarTascaBackend.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.ListenAnyIP(8080));

// Jwt config (fuente única)
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new InvalidOperationException("JWT_SECRET not set");
var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? throw new InvalidOperationException("JWT_ISSUER not set");
var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? throw new InvalidOperationException("JWT_AUDIENCE not set");

// Autenticación JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSecret)),
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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("VeteranOnly", policy => policy.RequireClaim("IsVeteran", "True"));
});

// DbContext
var connectionString =
    $"Server={Environment.GetEnvironmentVariable("MYSQL_HOST")};" +
    $"Port={Environment.GetEnvironmentVariable("MYSQL_PORT")};" +
    $"Database={Environment.GetEnvironmentVariable("MYSQL_DB")};" +
    $"Uid={Environment.GetEnvironmentVariable("MYSQL_USER")};" +
    $"Pwd={Environment.GetEnvironmentVariable("MYSQL_PASSWORD")};";
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("DB connection string not configured.");

builder.Services.AddDbContext<ColaDbContext>(opt =>
    opt.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Repos y servicios de aplicación
builder.Services.AddRepositories();
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<TicketMappingProfile>());
builder.Services.AddApplicationServices();
builder.Services.AddScoped<IStaffAuthService>(sp =>
{
    var repo = sp.GetRequiredService<IStaffUserRepository>();
    return new StaffAuthService(repo, jwtSecret, jwtIssuer, jwtAudience);
});

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
