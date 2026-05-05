using Microsoft.AspNetCore.Mvc;

namespace RetailPos.StoreManagement.Api.Controllers;

/// <summary>
/// Store & Terminal Management — manages store configurations,
/// terminal registration, cashier sessions, and open/close procedures.
/// </summary>
[ApiController]
[Route("api/v1/stores")]
public class StoreController : ControllerBase
{
    [HttpGet("{storeId}")]
    public IActionResult GetStore(string storeId) =>
        Ok(new { storeId, name = "Demo Store", status = "Open", terminals = 4 });

    [HttpGet("{storeId}/terminals")]
    public IActionResult GetTerminals(string storeId) =>
        Ok(new[] { new { terminalId = "T01", status = "Active", cashierId = "C001" } });

    [HttpPost("{storeId}/terminals/{terminalId}/open")]
    public IActionResult OpenTerminal(string storeId, string terminalId, [FromBody] OpenTerminalRequest req) =>
        Ok(new { sessionId = Guid.NewGuid(), openedAt = DateTimeOffset.UtcNow });

    [HttpPost("{storeId}/terminals/{terminalId}/close")]
    public IActionResult CloseTerminal(string storeId, string terminalId) =>
        Ok(new { closedAt = DateTimeOffset.UtcNow, totalSales = 0, drawerAmount = 0 });
}

public record OpenTerminalRequest(string CashierId, decimal OpeningFloat);
