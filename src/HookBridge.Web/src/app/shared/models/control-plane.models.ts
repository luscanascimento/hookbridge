import { DeliveryStatus } from '../../core/signalr/models/signalr.models';

export type EndpointStatus = 'Active' | 'Disabled';

export interface Application {
  id: string;
  name: string;
  description?: string | null;
  createdAt: string;
  endpointCount: number;
}

export interface Endpoint {
  id: string;
  applicationId: string;
  targetUrl: string;
  description?: string | null;
  status: EndpointStatus;
  rateLimitPerMinute: number;
  timeoutSeconds: number;
  createdAt: string;
  updatedAt?: string | null;
  subscriptionsCount: number;
}

export interface Subscription {
  id: string;
  endpointId: string;
  eventTypePattern: string;
  isActive: boolean;
  createdAt: string;
}

export interface DeliveryAttempt {
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

export interface Delivery {
  id: string;
  eventId: string;
  endpointId: string;
  endpointUrl?: string;
  subscriptionId: string;
  eventType: string;
  status: DeliveryStatus;
  attemptCount: number;
  scheduledAt: string;
  completedAt?: string | null;
  correlationId: string;
  traceParent?: string | null;
  originalDeliveryId?: string | null;
}

export interface DeliveryStats {
  totalDeliveries: number;
  successfulDeliveries: number;
  failedDeliveries: number;
  pendingDeliveries: number;
  deadLetteredDeliveries: number;
  successRatePercentage: number;
  averageLatencyMs: number;
}
