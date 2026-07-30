using Prometheus;

namespace ApiMediadorCascaron.Application.Metrics;

/// <summary>
/// Centraliza métricas custom del dominio de proxy/mediador para Prometheus.
/// </summary>
public static class ProxyMetrics
{
    /// <summary>
    /// Contador de hits de caché Redis cuando <c>update=false</c> y existe valor.
    /// </summary>
    public static readonly Counter CacheHitCounter = Prometheus.Metrics.CreateCounter(
        "proxy_cache_hit_total",
        "Total de respuestas entregadas desde cache Redis.");

    /// <summary>
    /// Contador de misses de caché Redis cuando <c>update=false</c> y no existe valor.
    /// </summary>
    public static readonly Counter CacheMissCounter = Prometheus.Metrics.CreateCounter(
        "proxy_cache_miss_total",
        "Total de cache misses que forzaron llamada externa.");

    /// <summary>
    /// Contador de errores al interactuar con Redis (lectura/escritura/conectividad).
    /// </summary>
    public static readonly Counter RedisErrorCounter = Prometheus.Metrics.CreateCounter(
        "proxy_redis_error_total",
        "Total de errores de Redis durante operaciones de cache.");

    /// <summary>
    /// Histograma de latencia de llamada al servicio externo en segundos.
    /// </summary>
    public static readonly Histogram ExternalLatencyHistogram = Prometheus.Metrics.CreateHistogram(
        "proxy_external_request_duration_seconds",
        "Latencia de llamadas HTTP al servicio externo.",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(start: 0.005, factor: 2, count: 12)
        });
}
