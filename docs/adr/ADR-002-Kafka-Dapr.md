# ADR-002: Apache Kafka as Event Backbone, Dapr as Abstraction

**Date:** 2025-05-05  
**Status:** Accepted  

## Context

Services need to communicate asynchronously with guaranteed delivery, ordering within a partition, and replay capability. Multiple messaging technologies exist (RabbitMQ, Azure Service Bus, AWS SNS/SQS, NATS). The platform must be cloud-agnostic.

## Decision

Use **Apache Kafka** as the durable, partitioned event log — deployed via **Strimzi Operator** on Kubernetes (cloud-agnostic).

Use **Dapr pub/sub** as the abstraction layer — services code against the Dapr SDK, not the Kafka SDK directly. This means:
- Services can run with Kafka in production
- Services can run with in-memory pub/sub in local dev
- Services can switch to Azure Service Bus by changing the Dapr component YAML (no code change)

## Consequences

**Positive:**
- Cloud-agnostic: change broker by swapping a YAML file, not code
- Kafka's partitioned log enables event replay and ordering guarantees
- Strimzi runs on any CNCF-compliant Kubernetes (AKS, EKS, GKE, bare metal)
- Dapr also handles retries, dead-letter, and circuit breaking at the sidecar level
- Schema Registry enforces event contract compatibility

**Negative:**
- Dapr adds a sidecar to every pod (CPU/memory overhead: ~100m/64Mi per pod)
- Kafka is operationally complex (ZooKeeper, topic management, consumer lag monitoring)
- Dapr abstraction hides Kafka-specific features (compacted topics, transactions)

## Alternatives Considered

| Option | Rejected Reason |
|---|---|
| RabbitMQ | No log replay, no ordered partitions |
| Azure Service Bus | Cloud-specific, vendor lock-in |
| NATS JetStream | Less mature, smaller ecosystem |
| Direct Kafka SDK | Cloud-locked, hard to test without Kafka running |

## Notes

- Schema Registry enforces Avro or JSON Schema compatibility
- Consumer groups scoped per service (not per microservice instance)
- Partition key = tenant-id ensures per-tenant ordering
- Dead Letter Queue: Kafka DLQ topic per service (`retail.{service}.dlq`)
