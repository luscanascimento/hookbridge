# HookBridge — Changelog

All notable changes to HookBridge are documented in this file in accordance with [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) standards.

## [1.0.0-rc.1] - 2026-09-03

### Added
- **Core Architecture (.NET 10 / ASP.NET Core 10):**
  - Clean Architecture implementation (`HookBridge.Domain`, `HookBridge.Application`, `HookBridge.Infrastructure`, `HookBridge.Api`).
  - RFC 7807 `ProblemDetails` exception handling pipeline and security headers middleware.
- **Authentication & Multi-Tenant Authorization:**
  - PBKDF2 HMAC-SHA256 password hashing (100k iterations) and cryptographic JWT issuance.
  - Refresh token rotation with reuse detection and instant family revocation.
  - Role-Based Access Control (TenantAdmin, Developer, Viewer, SystemOperator).
- **Webhook Control Plane:**
  - Full CRUD and lifecycle governance for Applications, Endpoints, Subscriptions, API Keys, and Webhook Secrets.
  - AES-256-GCM authenticated encryption for webhook signing secrets at rest.
  - SSRF protection guard blocking private RFC 1918 subnets, link-local, loopback, and cloud metadata (`169.254.169.254`).
- **Cryptographic Webhook Signing:**
  - Zero-allocation HMAC-SHA256 signature generator (`X-HookBridge-Signature: t=...,v1=...`).
  - Zero-downtime secret rotation supporting multi-signature headers during grace periods.
  - Anti-replay timestamp tolerance verification ($\le 300\text{s}$) with constant-time verification.
- **Event Publishing & Deliveries:**
  - EventFlow transactional outbox integration via resilient HTTP client (`IEventFlowClient`).
  - Granular subscription matching supporting exact, wildcard (`*`), and prefix (`order.*`) patterns.
  - Complete delivery and attempt lifecycle tracking with OpenTelemetry metric increments.
  - Authorized single and batch delivery replay engine with full ancestor lineage preservation.
  - DLQ management (peek, replay, purge) for dead-letter governance.
- **Real-Time Streaming (ASP.NET Core SignalR):**
  - Tenant-isolated SignalR hub (`/hubs/deliveries`) with WebSocket JWT query parameter authentication.
  - Application and endpoint granular subscription channels.
- **Developer Portal Frontend (Angular 22 Zoneless):**
  - High-density dark mode developer portal powered by Angular Signals.
  - Custom UI component suite (DataTables, Skeletons, Modals, SlideOvers, CodeViewer, Toast notifications).
  - Executive Dashboard with 4 KPI cards and 24-hour SVG throughput histogram.
  - Endpoint Management Portal with secret rotation wizard and testing drawer.
  - Live Event Inspector with real-time SignalR delivery stream.
  - Distributed Trace Explorer correlating events, deliveries, attempts, spans, and audit entries.
  - Payload Inspector with syntax highlighting, minification, Gzip compression estimator, and JSON Schema inference.
  - Endpoint Health & Reliability metrics with P50/P90/P95/P99 latency percentiles and circuit breaker state.
  - Event Schema manager supporting JSON Schema Draft 2020-12, versioning, and backward compatibility validation.
  - Webhook Sandbox with ephemeral webhook receivers for live payload capture.
  - Delivery Failure Simulator generating controlled responses (200, 429, 500, timeout, chaos).
- **Observability & OpenTelemetry:**
  - Complete OpenTelemetry tracing (`ActivitySource`), custom metrics (`Meter`), and structured logging.
  - In-memory circular telemetry buffer for instant UI trace exploration.
- **Containerization & Deployment:**
  - Production multi-stage Dockerfile for Backend API (non-root `app` user).
  - Production multi-stage Dockerfile for Angular Web UI with hardened Nginx reverse proxy.
  - Full-stack `docker-compose.yml` orchestrating API, Web, PostgreSQL 16, RabbitMQ 3.13, Redis 7.4, and Jaeger.
- **CI/CD Pipeline:**
  - GitHub Actions CI matrix for backend build/test with code coverage, frontend build/lint, and docker compose validation.
  - Security audit workflow scanning vulnerable dependencies and secret leaks.
  - Release workflow publishing multi-arch images to GitHub Container Registry (GHCR).

### Security
- Comprehensive IDOR, SSRF, XSS, and replay attack test suites with 100% pass rate.
