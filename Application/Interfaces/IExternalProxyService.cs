using System.Text.Json.Nodes;

namespace ApiMediadorCascaron.Application.Interfaces;

/// <summary>
/// Define el contrato de orquestación para el proxy hacia servicio externo con soporte de caché Redis.
/// </summary>
public interface IExternalProxyService
{
    /// <summary>
    /// Obtiene datos externos de un endpoint configurado con comportamiento condicional de caché.
    /// </summary>
    /// <param name="endpointKey">Identificador interno del endpoint configurado en appsettings.</param>
    /// <param name="parametro">Número de documento/parámetro requerido por el endpoint externo.</param>
    /// <param name="forceUpdate">Si es true, omite lectura de Redis y fuerza actualización desde origen externo.</param>
    /// <param name="cancellationToken">Token de cancelación de la operación asíncrona.</param>
    /// <returns>Respuesta JSON normalizada del origen externo o desde caché.</returns>
    Task<JsonNode> GetFromEndpointAsync(string endpointKey, string parametro, bool forceUpdate, CancellationToken cancellationToken);
}
