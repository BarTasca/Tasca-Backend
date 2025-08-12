using BarTasca.Data;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;

// Cargar .env
DotNetEnv.Env.Load();

// Configuración del entorno
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(8080);
});

// Add services to the container.
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DbContext con MySQL desde variable de entorno
var connectionString = $"Server={Environment.GetEnvironmentVariable("MYSQL_HOST")};" +
                       $"Port={Environment.GetEnvironmentVariable("MYSQL_PORT")};" +
                       $"Database={Environment.GetEnvironmentVariable("MYSQL_DB")};" +
                       $"Uid={Environment.GetEnvironmentVariable("MYSQL_USER")};" +
                       $"Pwd={Environment.GetEnvironmentVariable("MYSQL_PASSWORD")};";

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("La cadena de conexión a la base de datos no está configurada correctamente.");
}

builder.Services.AddDbContext<ColaDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// SignalR (placeholder para más adelante)
builder.Services.AddSignalR();

// Repositorios (Data)
builder.Services.AddRepositories();

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
// app.MapHub<QueueHub>("/queueHub"); // <- cuando tengas el Hub

app.Run();
