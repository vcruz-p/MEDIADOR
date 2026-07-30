using ApiMediadorCascaron.Application.Interfaces;
using ApiMediadorCascaron.Application.Services;
using Microsoft.OpenApi.Models;
using Prometheus;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Carga de configuración tipada para valores de infraestructura.
var redisConnectionString = builder.Configuration["Redis:ConnectionString"]
    ?? throw new InvalidOperationException("Redis:ConnectionString is not configured.");

// Servicios base para API y serialización JSON.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API Mediador Cascarón",
        Version = "v1",
        Description = "Plantilla reusable de API Proxy/Mediador con Clean Architecture."
    });
});

// ProblemDetails para respuestas de error consistentes.
builder.Services.AddProblemDetails();

// CORS abierto en todos los entornos (Development/Production).
builder.Services.AddCors(options =>
{
    options.AddPolicy("OpenCorsPolicy", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Registro de Redis como singleton para reutilizar la conexión.
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
    // Permite que la app continúe operativa aunque Redis no esté disponible al arranque.
    redisOptions.AbortOnConnectFail = false;
    redisOptions.ConnectRetry = 3;
    redisOptions.ConnectTimeout = builder.Configuration.GetValue<int?>("Redis:ConnectTimeoutMs") ?? 10000;
    return ConnectionMultiplexer.Connect(redisOptions);
});

// Registro del servicio de aplicación con HttpClient tipado.
builder.Services.AddHttpClient<IExternalProxyService, ExternalProxyService>((serviceProvider, httpClient) =>
{
    var timeoutSeconds = builder.Configuration.GetValue<int?>("ExternalApi:TimeoutSeconds") ?? 120;
    httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});

var app = builder.Build();

// Swagger habilitado globalmente, sin condicionales por entorno.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "API Mediador Cascarón v1");
    options.RoutePrefix = "swagger";
});

app.UseCors("OpenCorsPolicy");

// Métricas HTTP automáticas de ASP.NET Core para Prometheus.
app.UseHttpMetrics();

app.MapControllers();

// Endpoint de scraping Prometheus sin autenticación.
app.MapMetrics("/metrics");

app.Run();
