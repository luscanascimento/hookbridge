# ADR 0004: Angular 22 Zoneless and Signal-Driven Architecture

## Status
Accepted

## Context
The HookBridge Developer Portal requires high rendering performance, low memory footprint, and instantaneous UI updates from real-time SignalR telemetry streams without the monkey-patching overhead and change detection quirks of `zone.js`.

## Decision
We adopt **Angular 22 with Zoneless Change Detection and Signal Reactivity**:
1. Disable `zone.js` in favor of `provideExperimentalZonelessChangeDetection()`.
2. Model all UI and component state using Angular Signals (`signal`, `computed`, `input`, `output`).
3. Use modern control flow syntax (`@if`, `@for`, `@switch`, `@defer`).
4. Adopt feature-based facades/services for state management instead of heavy global state libraries.

## Consequences
### Positive
- Faster initial load and runtime performance.
- Fine-grained reactivity without accidental full-tree re-renders.
- Clean, readable TypeScript templates.

### Trade-offs
- Developers must follow signal-based reactivity conventions and avoid manual `NgZone` patterns.

