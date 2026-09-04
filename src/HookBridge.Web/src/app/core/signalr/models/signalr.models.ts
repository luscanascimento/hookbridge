export type HubConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export type DeliveryStatus = 'Pending' | 'Dispatched' | 'Success' | 'Failed' | 'DeadLettered';

export interface AttemptSummary {
  id: string;
  deliveryId: string;
  attemptNumber: number;
  httpStatusCode: number;
  requestHeadersJson: string;
  requestBody: string;
  responseHeadersJson?: string | null;
  responseBody?: string | null;
  elapsedMs: number;
  errorMessage?: string | null;
  executedAt: string;
}

export interface RealtimeDeliveryEvent {
  eventType: 'DeliveryDispatched' | 'DeliveryAttemptRecorded' | 'DeliveryReplayed';
  deliveryId: string;
  tenantId: string;
  endpointId: string;
  eventName: string;
  status: DeliveryStatus;
  attemptCount: number;
  correlationId: string;
  traceParent?: string | null;
  timestamp: string;
  attempt?: AttemptSummary | null;
  originalDeliveryId?: string | null;
}
