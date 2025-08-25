using BarTasca.Data;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;
using BarTasca.Services.Mapping;
using AutoMapper;
using BarTasca.Services;
using BarTascaBackend;
using BarTascaBackend.Hubs;
using BarTasca.Infrastructure;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.ListenAnyIP(8080));

builder.Services.AddApiLayer();

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

// SignalR + Infrastructure (inyecta el Hub real que usará el notifier)
builder.Services.AddSignalR();
builder.Services.AddInfrastructure<QueueHub>(); // <--- aquí se resuelve INotificationService

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Sirve archivos estáticos desde wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

//app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapHub<QueueHub>("/hubs/queue");

app.Run();
