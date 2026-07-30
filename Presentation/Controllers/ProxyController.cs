using ApiMediadorCascaron.Application.DTOs;
using ApiMediadorCascaron.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace ApiMediadorCascaron.Presentation.Controllers;

/// <summary>
/// Expone endpoints HTTP para operaciones de mediador/proxy.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class ProxyController : ControllerBase
{
    private readonly IExternalProxyService _externalProxyService;
    private readonly ILogger<ProxyController> _logger;

    /// <summary>
    /// Constructor principal del controlador de proxy.
    /// </summary>
    public ProxyController(IExternalProxyService externalProxyService, ILogger<ProxyController> logger)
    {
        _externalProxyService = externalProxyService;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene registros independientes usando caché Redis y permite refresh forzado con <c>forceUpdate</c>.
    /// </summary>
    /// <param name="request">Parámetros de entrada del proxy.</param>
    /// <param name="cancellationToken">Token de cancelación de la petición HTTP.</param>
    /// <returns>Payload JSON del recurso mediado.</returns>
    [HttpGet("registros-independientes")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRegistrosIndependientesAsync([FromQuery] ProxyRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteProxyAsync("obtener-RegistrosIndependientes", request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Obtiene registros independientes usando caché Redis y permite refresh forzado con <c>forceUpdate</c>.
    /// </summary>
    /// <param name="request">Parámetros de entrada del proxy.</param>
    /// <param name="cancellationToken">Token de cancelación de la petición HTTP.</param>
    /// <returns>Payload JSON del recurso mediado.</returns>
    [HttpGet("obtener-RegistrosIndependientes")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetObtenerRegistrosIndependientesAsync([FromQuery] ProxyRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteProxyAsync("obtener-RegistrosIndependientes", request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Obtiene información de persona usando caché Redis y permite refresh forzado con <c>forceUpdate</c>.
    /// </summary>
    /// <param name="request">Parámetros de entrada del proxy.</param>
    /// <param name="cancellationToken">Token de cancelación de la petición HTTP.</param>
    /// <returns>Payload JSON del recurso mediado.</returns>
    [HttpGet("obtener-persona")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPersonaAsync([FromQuery] ProxyRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteProxyAsync("obtener-persona", request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Obtiene información de persona con foto usando caché Redis y permite refresh forzado con <c>forceUpdate</c>.
    /// </summary>
    /// <param name="request">Parámetros de entrada del proxy.</param>
    /// <param name="cancellationToken">Token de cancelación de la petición HTTP.</param>
    /// <returns>Payload JSON del recurso mediado.</returns>
    [HttpGet("obtener-persona-con-foto")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPersonaConFotoAsync([FromQuery] ProxyRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteProxyAsync("obtener-persona-con-foto", request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ejecuta el flujo estándar de proxy para el endpoint solicitado.
    /// </summary>
    private async Task<IActionResult> ExecuteProxyAsync(string endpointKey, ProxyRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Parametro))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid parameter",
                Detail = "The query parameter 'parametro' is required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        try
        {
            var response = await _externalProxyService
                .GetFromEndpointAsync(endpointKey, request.Parametro, request.ForceUpdate, cancellationToken)
                .ConfigureAwait(false);

            return Ok(response);
        }
        catch (ArgumentException argumentException)
        {
            _logger.LogWarning(argumentException, "Invalid request parameters for proxy endpoint.");
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid parameter",
                Detail = argumentException.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (HttpRequestException httpRequestException)
        {
            _logger.LogError(httpRequestException, "External HTTP call failed.");
            return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Title = "Bad Gateway",
                Detail = "The external dependency returned an error.",
                Status = StatusCodes.Status502BadGateway
            });
        }
        catch (TaskCanceledException timeoutException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(timeoutException, "External HTTP call timed out.");
            return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Title = "Bad Gateway",
                Detail = "The external dependency timed out.",
                Status = StatusCodes.Status502BadGateway
            });
        }
        catch (JsonException jsonException)
        {
            _logger.LogError(jsonException, "Invalid JSON payload from cache or external API.");
            return StatusCode((int)HttpStatusCode.BadGateway, new ProblemDetails
            {
                Title = "Bad Gateway",
                Detail = "The external dependency returned malformed JSON.",
                Status = StatusCodes.Status502BadGateway
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error while processing proxy request.");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }
}
