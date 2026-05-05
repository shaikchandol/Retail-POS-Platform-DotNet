# ADR-005: Zero-Trust Networking with Dapr mTLS + Kubernetes NetworkPolicy

**Status:** Accepted  
**Date:** 2025-01  
**Deciders:** Platform Architecture Board  

---

## Context

Service-to-service calls traverse a shared Kubernetes cluster. Without explicit
controls, any pod can reach any other pod — this violates PCI DSS requirement 1
(network segmentation) and Zero-Trust principles.

## Decision

Implement Zero-Trust networking using:

1. **Dapr Sentry** as the internal mTLS Certificate Authority (SPIFFE/X.509)
2. **Kubernetes NetworkPolicy** (default deny-all, explicit allow per service)
3. **Isolated `pci` namespace** for the Tokenization API (only `payments-service` may ingress)
4. **JWT forwarding** for identity propagation across service boundaries

## Consequences

**Positive:**
- No implicit trust — all east-west traffic is encrypted and authenticated
- PCI DSS network segmentation requirement satisfied without additional tooling
- Dapr handles cert issuance and rotation automatically
- Audit trail via Dapr access logs

**Negative:**
- mTLS adds ~1ms latency per hop (acceptable)
- Certificate rotation requires Dapr Sentry availability
- NetworkPolicy debugging requires `kubectl` access

## Alternatives Rejected

| Alternative | Reason Rejected |
|---|---|
| Istio service mesh | Heavier operational overhead; Dapr already in stack |
| Cilium eBPF policies | Requires specific CNI — reduces cloud-agnosticism |
| Application-layer auth only | Insufficient for PCI; no encryption at transport layer |

## Swap Point

Replace Dapr Sentry with SPIRE (SPIFFE Runtime Environment) for multi-cluster
or multi-cloud identity — swap the Dapr mTLS configuration in `dapr-tracing.yaml`.
No application code changes required.
