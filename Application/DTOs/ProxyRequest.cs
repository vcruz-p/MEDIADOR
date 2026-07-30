namespace ApiMediadorCascaron.Application.DTOs;

/// <summary>
/// Modelo de entrada para el endpoint proxy.
/// </summary>
/// <param name="Parametro">Valor de entrada para el query string del servicio externo.</param>
/// <param name="ForceUpdate">Bandera que fuerza consulta externa y actualización de caché.</param>
public sealed record ProxyRequest(string Parametro, bool ForceUpdate = false);
