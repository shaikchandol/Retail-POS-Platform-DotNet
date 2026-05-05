# ADR-004: Podman (Rootless) as Container Build Tool

**Date:** 2025-05-05  
**Status:** Accepted  

## Context

CI/CD pipelines need to build OCI-compliant container images. Docker requires a daemon running as root, creating security concerns in shared build agents and violating least-privilege principles.

## Decision

Use **Podman** (rootless mode) for all container builds in Azure DevOps pipelines.

```yaml
# No daemon required — rootless, daemonless, fork-exec model
podman build \
  --file Dockerfile \
  --tag registry.example.com/sales-service:${BUILD_ID} \
  .
```

## Consequences

**Positive:**
- No root required — reduces attack surface in build agents
- Daemonless — no persistent background process to manage
- OCI-compliant images — identical to Docker images, run on any container runtime
- Compatible with Kubernetes (containerd, CRI-O)
- Works in rootless containers (nested containerization in pipelines)

**Negative:**
- Some Docker Compose workflows need podman-compose substitute
- Minor CLI differences (e.g., `--format=docker` flag for Docker Hub compat)
- Build cache less mature than Docker BuildKit (mitigated by layer caching)

## Dockerfile Best Practices (Applied)

```dockerfile
# Multi-stage build — minimal final image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# Non-root user
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "RetailPos.Sales.Api.dll"]
```
