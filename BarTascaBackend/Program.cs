using BarTasca.Data;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;
using BarTasca.Services.Mapping;
using AutoMapper;
using BarTasca.Services;
using BarTascaBackend;

// Cargar .env
DotNetEnv.Env.Load();

// Configuración del entorno
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(8080);
});

// API (controllers + swagger + signalR)
builder.Services.AddApiLayer();

// DbContext con MySQL desde variables de entorno
var connectionString = $"Server={Environment.GetEnvironmentVariable("MYSQL_HOST")};" +
                       $"Port={Environment.GetEnvironmentVariable("MYSQL_PORT")};" +
                       $"Database={Environment.GetEnvironmentVariable("MYSQL_DB")};" +
                       $"Uid={Environment.GetEnvironmentVariable("MYSQL_USER")};" +
                       $"Pwd={Environment.GetEnvironmentVariable("MYSQL_PASSWORD")};";

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("La cadena de conexión a la base de datos no está configurada correctamente.");

builder.Services.AddDbContext<ColaDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Repositorios (Data)
builder.Services.AddRepositories();

// AutoMapper (si tu Profile está en DTOs/Mapping como hasta ahora)
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<TicketMappingProfile>());

// Servicios de aplicación (Services)
builder.Services.AddApplicationServices();

var app = builder.Build();

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
// app.MapHub<QueueHub>("/queueHub");

app.Run();
