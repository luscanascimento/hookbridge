import { DeliveryDetail } from '../../core/services/delivery.service';

export interface TraceSpanEvent {
  name: string;
  timestamp: string;
  attributes?: Record<string, string> | null;
}

export interface TraceSpan {
  spanId: string;
  parentSpanId?: string | null;
  name: string;
  service: string;
  kind: 'Server' | 'Producer' | 'Consumer' | 'Client' | 'Internal' | string;
  startTime: string;
  durationMs: number;
  offsetMs: number;
  status: 'Ok' | 'Error' | 'Pending' | string;
  attributes: Record<string, string>;
  events: TraceSpanEvent[];
}

export interface TraceRootEvent {
  eventId?: string | null;
  eventType: string;
  timestamp: string;
  correlationId: string;
  idempotencyKey?: string | null;
  payloadPreview?: string | null;
}

export interface TraceSummary {
  traceId: string;
  correlationId: string;
  traceParent?: string | null;
  eventType: string;
  initiatedAt: string;
  completedAt?: string | null;
  totalDurationMs: number;
  status: string;
  spanCount: number;
  deliveryCount: number;
  attemptCount: number;
  auditCount: number;
}

export interface AuditEntry {
  id: string;
  tenantId: string;
  userId?: string | null;
  action: string;
  resourceType: string;
  resourceId: string;
  detailsJson: string;
  ipAddress?: string | null;
  traceId?: string | null;
  timestamp: string;
}

export interface TraceDetail {
  traceId: string;
  correlationId: string;
  traceParent?: string | null;
  eventType: string;
  initiatedAt: string;
  completedAt?: string | null;
  totalDurationMs: number;
  overallStatus: string;
  rootEvent?: TraceRootEvent | null;
  spans: TraceSpan[];
  deliveries: DeliveryDetail[];
  auditLogs: AuditEntry[];
}

export interface TraceQueryParams {
  query?: string | null;
  status?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
  page?: number;
  pageSize?: number;
}
