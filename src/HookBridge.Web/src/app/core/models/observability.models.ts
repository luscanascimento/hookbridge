export interface ObservabilitySummary {
  serviceName: string;
  serviceVersion: string;
  environment: string;
  otlpExporterConfigured: boolean;
  otlpEndpoint?: string | null;
  registeredInstrumentsCount: number;
  totalRecordedSpansCount: number;
  processWorkingSetMb: number;
  processHeapMb: number;
  gcGen0Collections: number;
  gcGen1Collections: number;
  gcGen2Collections: number;
  metricCounters: Record<string, number>;
  timestamp: string;
}

export interface MetricInstrument {
  name: string;
  unit: string;
  description: string;
  type: string;
  currentValue: number;
}

export interface CapturedSpan {
  traceId: string;
  spanId: string;
  parentSpanId?: string | null;
  operationName: string;
  sourceName: string;
  durationMs: number;
  startTime: string;
  status: string;
  tags: Record<string, string>;
  baggage: Record<string, string>;
}

export interface SyntheticTraceCommand {
  eventType: string;
  includeFailure: boolean;
  delayMs: number;
}

export interface SyntheticTraceResult {
  traceId: string;
  rootSpanId: string;
  totalSpansGenerated: number;
  totalDurationMs: number;
  spans: CapturedSpan[];
}
