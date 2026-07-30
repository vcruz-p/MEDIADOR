using ApiMediadorCascaron.Application.Interfaces;
using ApiMediadorCascaron.Application.Metrics;
using Microsoft.AspNetCore.WebUtilities;
using StackExchange.Redis;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ApiMediadorCascaron.Application.Services;

/// <summary>
/// Implementa la lógica de mediación/proxy con lectura/escritura en Redis.
/// </summary>
public sealed class ExternalProxyService : IExternalProxyService
{
    private readonly HttpClient _httpClient;
    private readonly IDatabase _redisDatabase;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExternalProxyService> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de proxy.
    /// </summary>
    public ExternalProxyService(
        HttpClient httpClient,
        IConnectionMultiplexer connectionMultiplexer,
        IConfiguration configuration,
        ILogger<ExternalProxyService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        // Permite seleccionar la base de datos lógica de Redis vía appsettings.
        var databaseId = _configuration.GetValue<int?>("Redis:DatabaseId") ?? 0;
        _redisDatabase = connectionMultiplexer.GetDatabase(databaseId);
    }

    /// <inheritdoc />
    public async Task<JsonNode> GetFromEndpointAsync(string endpointKey, string parametro, bool forceUpdate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(endpointKey))
        {
            throw new ArgumentException("The endpoint key is required.", nameof(endpointKey));
        }

        if (string.IsNullOrWhiteSpace(parametro))
        {
            throw new ArgumentException("The parametro value is required.", nameof(parametro));
        }

        var endpointSection = _configuration.GetSection($"ExternalApi:Endpoints:{endpointKey}");
        var externalEndpointUrl = endpointSection["Url"]
            ?? throw new InvalidOperationException($"ExternalApi:Endpoints:{endpointKey}:Url is not configured.");
        var ttlDays = endpointSection.GetValue<double?>("TtlDays")
            ?? _configuration.GetValue<double?>("Redis:DefaultTtlDays")
            ?? 1;

        var redisCompositeKey = $"persona:{endpointKey}:{parametro}";

        // Requisito: si forceUpdate=false se debe intentar resolver desde Redis antes de consultar externo.
        if (!forceUpdate)
        {
            try
            {
                var cachedValue = await _redisDatabase.StringGetAsync(redisCompositeKey).ConfigureAwait(false);
                if (cachedValue.HasValue)
                {
                    ProxyMetrics.CacheHitCounter.Inc();
                    return JsonNode.Parse(cachedValue.ToString())
                        ?? throw new JsonException("Cached value is not a valid JSON payload.");
                }

                ProxyMetrics.CacheMissCounter.Inc();
            }
            catch (RedisException redisException)
            {
                ProxyMetrics.RedisErrorCounter.Inc();
                _logger.LogWarning(redisException, "Redis read failed for key {RedisKey}. Continuing with external source.", redisCompositeKey);
            }
        }

        var queryParamName = _configuration["ExternalApi:CacheKeyQueryParam"] ?? "parametro";
        var requestUrl = QueryHelpers.AddQueryString(externalEndpointUrl, queryParamName, parametro);

        var stopwatch = Stopwatch.StartNew();
        using var response = await _httpClient.GetAsync(requestUrl, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();
        ProxyMetrics.ExternalLatencyHistogram.Observe(stopwatch.Elapsed.TotalSeconds);

        response.EnsureSuccessStatusCode();

        var externalPayload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var payloadNode = JsonNode.Parse(externalPayload)
            ?? throw new JsonException("External response is not a valid JSON payload.");

        // 🛠️ TRANSFORMACIÓN DE RESPUESTA:
        // Aquí puedes mapear, enriquecer o agregar campos al JSON externo antes de persistir en Redis.
        // Ejemplo:
        // if (payloadNode is JsonObject jsonObject)
        // {
        //     jsonObject["proxyProcessedAtUtc"] = DateTime.UtcNow;
        //     jsonObject["source"] = "external-api";
        // }

        var normalizedJson = payloadNode.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = false
        });

        // Requisito: siempre persistir/sobrescribir en Redis cuando se consulta el origen externo.
        try
        {
            await _redisDatabase.StringSetAsync(
                redisCompositeKey,
                normalizedJson,
                TimeSpan.FromDays(ttlDays)).ConfigureAwait(false);
        }
        catch (RedisException redisException)
        {
            ProxyMetrics.RedisErrorCounter.Inc();
            _logger.LogWarning(redisException, "Redis write failed for key {RedisKey}. Returning external payload without cache update.", redisCompositeKey);
        }

        return payloadNode;
    }
}
