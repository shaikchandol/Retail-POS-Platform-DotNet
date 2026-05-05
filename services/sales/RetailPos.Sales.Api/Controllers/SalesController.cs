using MediatR;
using Microsoft.AspNetCore.Mvc;
using RetailPos.Sales.Application.Features.CreateSale;
using RetailPos.Sales.Application.Features.GetSale;
using RetailPos.Sales.Application.Features.VoidSale;

namespace RetailPos.Sales.Api.Controllers;

/// <summary>
/// Sales API — one controller per vertical slice.
/// Thin: delegates all logic to MediatR commands/queries.
/// </summary>
[ApiController]
[Route("api/v1/sales")]
[Produces("application/json")]
public class SalesController(IMediator mediator) : ControllerBase
{
    /// <summary>Create and complete a sale (atomic checkout)</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateSaleResponse), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateSale([FromBody] CreateSaleCommand cmd, CancellationToken ct)
    {
        var result = await mediator.Send(cmd, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetSale), new { id = result.Value!.SaleId }, result.Value)
            : BadRequest(new { error = result.ErrorCode, message = result.Error });
    }

    /// <summary>Get a sale by ID (from read model)</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetSale(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetSaleQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.ErrorCode, message = result.Error });
    }

    /// <summary>Get sales history (paged, filterable)</summary>
    [HttpGet]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string? storeId, [FromQuery] string? terminalId,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1, [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetSalesHistoryQuery(storeId, terminalId, from, to, page, limit), ct);
        return Ok(result.Value);
    }

    /// <summary>Void an active sale</summary>
    [HttpPost("{id:guid}/void")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> VoidSale(Guid id, [FromBody] VoidSaleRequest body, CancellationToken ct)
    {
        var result = await mediator.Send(new VoidSaleCommand(id, body.Reason, body.AuthorizedBy), ct);
        if (!result.IsSuccess)
            return result.ErrorCode == "NOT_FOUND" ? NotFound() : BadRequest(new { error = result.ErrorCode, message = result.Error });
        return NoContent();
    }
}

public record VoidSaleRequest(string Reason, string? AuthorizedBy);
