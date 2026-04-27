using System.ComponentModel.DataAnnotations;
using AuditLogService.Application.Commands.RecordAuditEvent;
using AuditLogService.Application.DTOs;
using AuditLogService.Application.Queries.GetAuditEvents;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AuditLogService.Api.Controllers;

[ApiController]
[Route("api/audit-events")]
public sealed class AuditEventsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<AuditEventDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Record(
        [FromBody] RecordAuditEventRequest request,
        CancellationToken ct)
    {
        var correlationId = HttpContext.Items["CorrelationId"] as string;

        var command = new RecordAuditEventCommand(
            Actor: request.Actor,
            Action: request.Action,
            Resource: request.Resource,
            ResourceId: request.ResourceId,
            CorrelationId: request.CorrelationId ?? correlationId,
            Metadata: request.Metadata);

        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(Query), new { }, result);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AuditEventDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Query(
        [FromQuery] string? actor,
        [FromQuery] string? action,
        [FromQuery] string? resource,
        [FromQuery] string? correlationId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery, Range(1, 200)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        CancellationToken ct = default)
    {
        var query = new GetAuditEventsQuery(actor, action, resource, correlationId, fromUtc, toUtc, pageSize, page);
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }
}

public sealed record RecordAuditEventRequest(
    [Required, StringLength(256, MinimumLength = 1)] string Actor,
    [Required, StringLength(256, MinimumLength = 1)] string Action,
    [Required, StringLength(256, MinimumLength = 1)] string Resource,
    [StringLength(256)] string? ResourceId,
    [StringLength(128)] string? CorrelationId,
    Dictionary<string, string>? Metadata);
