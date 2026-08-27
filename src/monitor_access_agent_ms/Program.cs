using DotNetEnv;
using monitor_access_agent_ms.Domain.Interfaces;
using monitor_access_agent_ms.Infrastructure.Persistence.Context;
using monitor_access_agent_ms.Infrastructure.Repositories;
using monitor_access_agent_ms.libs;
using MicroservicesTemplate.Domain.Repositories;
using MicroservicesTemplate.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Cargar variables del .env
Env.Load();

// Obtener cadena de conexión
var connectionString = EnvironmentConfiguration.GetConnectionString();

// Agregar DbContext con cadena de conexión
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddGapInfrastructure<ApplicationDbContext>();

//Este comando ensambla todos los queries, commands y handlers de mi capa de aplicacion
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

//Configuracion de cors para poder permitir el acceso desde el frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev",
        policy => policy.WithOrigins("http://localhost:4200")  // URL de tu frontend
                        .AllowAnyHeader()
                        .AllowAnyMethod());
});

// Configuración estándar
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configuración genérica de Swagger
var projectName = Assembly.GetExecutingAssembly().GetName().Name ?? "API";
var apiTitle = $"{projectName} API";
var apiVersion = "v1";

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc(apiVersion, new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = apiTitle,
        Version = apiVersion,
        Description = $"API documentation for {projectName}",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Development Team",
            Email = "www.gapsystem.net"
        }
    });

    // Incluir comentarios XML si existen
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Aplicar el Path Base desde el .env
var apiRootPath = EnvironmentConfiguration.GetApiRootPath();
if (!string.IsNullOrEmpty(apiRootPath))
{
    app.UsePathBase(apiRootPath);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint($"{apiRootPath}/swagger/{apiVersion}/swagger.json", $"{apiTitle} {apiVersion.ToUpper()}");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = $"{apiTitle} - API Documentation";
    });
}


// Middleware y endpoints
app.UseAuthorization();
app.MapControllers();
app.Run();