FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore — separate layer for cache efficiency
COPY ["services/sales/RetailPos.Sales.Api/RetailPos.Sales.Api.csproj", "services/sales/RetailPos.Sales.Api/"]
COPY ["services/sales/RetailPos.Sales.Application/RetailPos.Sales.Application.csproj", "services/sales/RetailPos.Sales.Application/"]
COPY ["services/sales/RetailPos.Sales.Domain/RetailPos.Sales.Domain.csproj", "services/sales/RetailPos.Sales.Domain/"]
COPY ["services/sales/RetailPos.Sales.Infrastructure/RetailPos.Sales.Infrastructure.csproj", "services/sales/RetailPos.Sales.Infrastructure/"]
COPY ["building-blocks/RetailPos.BuildingBlocks.Domain/RetailPos.BuildingBlocks.Domain.csproj", "building-blocks/RetailPos.BuildingBlocks.Domain/"]
COPY ["building-blocks/RetailPos.BuildingBlocks.Application/RetailPos.BuildingBlocks.Application.csproj", "building-blocks/RetailPos.BuildingBlocks.Application/"]
COPY ["building-blocks/RetailPos.BuildingBlocks.EventSourcing/RetailPos.BuildingBlocks.EventSourcing.csproj", "building-blocks/RetailPos.BuildingBlocks.EventSourcing/"]
COPY ["building-blocks/RetailPos.BuildingBlocks.MultiTenancy/RetailPos.BuildingBlocks.MultiTenancy.csproj", "building-blocks/RetailPos.BuildingBlocks.MultiTenancy/"]
COPY ["building-blocks/RetailPos.BuildingBlocks.Dapr/RetailPos.BuildingBlocks.Dapr.csproj", "building-blocks/RetailPos.BuildingBlocks.Dapr/"]

RUN dotnet restore services/sales/RetailPos.Sales.Api/RetailPos.Sales.Api.csproj

COPY . .
RUN dotnet publish services/sales/RetailPos.Sales.Api/RetailPos.Sales.Api.csproj \
    -c Release -o /app --no-restore

# ── Runtime image ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Security: non-root user
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

WORKDIR /app
COPY --from=build /app .

# Security: non-root execution
USER appuser

# Expose only app port (Dapr sidecar uses 3500/50001 — handled by K8s)
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "RetailPos.Sales.Api.dll"]
