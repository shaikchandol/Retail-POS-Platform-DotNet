using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RetailPos.Sales.Application.Features.CreateSale;
using Xunit;

namespace RetailPos.Sales.IntegrationTests;

/// <summary>
/// Integration tests: spins up real Sales API with test infrastructure.
/// Requires: PostgreSQL running at localhost:5432 (use TestContainers in CI).
/// </summary>
[Collection("Integration")]
public class CreateSaleIntegrationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateSale_ValidRequest_Returns201WithReceiptNumber()
    {
        // Arrange
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", "test-tenant");
        _client.DefaultRequestHeaders.Add("X-Store-Id", "store-01");
        _client.DefaultRequestHeaders.Add("X-Terminal-Id", "terminal-01");

        var cmd = new CreateSaleCommand
        {
            CustomerId = "customer-123",
            PaymentMethod = "CARD",
            Currency = "USD",
            Items =
            [
                new SaleItemRequest("prod-001", "Widget A", "WGT-A", 2, 10.00m, 0.1m),
                new SaleItemRequest("prod-002", "Widget B", "WGT-B", 1, 25.00m, 0.1m),
            ]
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/sales", cmd);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateSaleResponse>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.SaleId);
        Assert.NotEmpty(result.ReceiptNumber);
        Assert.Equal("USD", result.Currency);
        Assert.True(result.TotalAmount > 0);
    }

    [Fact]
    public async Task CreateSale_MissingTenantHeader_Returns400()
    {
        var cmd = new CreateSaleCommand
        {
            CustomerId = "c1", PaymentMethod = "CASH",
            Items = [new SaleItemRequest("p1", "P", "P-1", 1, 5m, 0)]
        };

        var response = await _client.PostAsJsonAsync("/api/v1/sales", cmd);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSale_EmptyItems_Returns422()
    {
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", "test-tenant");
        var cmd = new CreateSaleCommand
        {
            CustomerId = "c1", PaymentMethod = "CASH", Items = []
        };

        var response = await _client.PostAsJsonAsync("/api/v1/sales", cmd);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
