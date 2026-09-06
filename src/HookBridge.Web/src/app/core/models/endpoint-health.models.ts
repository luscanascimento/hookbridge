export interface LatencyQuantiles {
  averageMs: number;
  p50Ms: number;
  p90Ms: number;
  p95Ms: number;
  p99Ms: number;
  minMs: number;
  maxMs: number;
}

export interface EndpointHourlyHealthBucket {
  timestamp: string;
  totalDeliveries: number;
  successCount: number;
  failedCount: number;
  deadLetteredCount: number;
  averageLatencyMs: number;
  successRatePercent: number;
  healthScore: number;
}

export type IncidentSeverity = 'Critical' | 'Warning' | 'Info';

export interface EndpointIncidentAlert {
  id: string;
  severity: IncidentSeverity;
  type: string;
  title: string;
  description: string;
  triggeredAt: string;
}

export type CircuitBreakerState = 'Closed' | 'HalfOpen' | 'Open';

export interface EndpointHealth {
  endpointId: string;
  targetUrl: string;
  description?: string | null;
  status: string;
  circuitState: CircuitBreakerState;
  healthScorePercent: number;
  uptimePercent: number;
  totalDeliveries: number;
  successCount: number;
  failedCount: number;
  deadLetteredCount: number;
  consecutiveFailures: number;
  errorRatePercent: number;
  lastDeliveryAt?: string | null;
  lastFailureReason?: string | null;
  latencies: LatencyQuantiles;
  hourlyBuckets: EndpointHourlyHealthBucket[];
  incidents: EndpointIncidentAlert[];
}

export interface EndpointHealthSummary {
  endpointId: string;
  targetUrl: string;
  status: string;
  circuitState: CircuitBreakerState;
  healthScorePercent: number;
  uptimePercent: number;
  totalDeliveries: number;
  successRatePercent: number;
  p95LatencyMs: number;
  activeIncidentCount: number;
}

export interface TenantEndpointsHealthSummary {
  overallHealthScorePercent: number;
  overallUptimePercent: number;
  totalEndpoints: number;
  healthyCount: number;
  degradedCount: number;
  criticalCount: number;
  totalOpenCircuits: number;
  activeIncidentCount: number;
  endpoints: EndpointHealthSummary[];
}
