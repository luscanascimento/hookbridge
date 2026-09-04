import { DeliveryStatus } from '../../core/signalr/models/signalr.models';

export type EndpointStatus = 'Active' | 'Paused' | 'Disabled';
export type SecretStatus = 'Active' | 'Rotating' | 'Revoked';

export interface Application {
  id: string;
  name: string;
  description?: string | null;
  createdAt: string;
  endpointCount?: number;
}

export interface Endpoint {
  id: string;
  tenantId: string;
  applicationId: string;
  targetUrl: string;
  description?: string | null;
  status: EndpointStatus;
  disabledReason?: string | null;
  rateLimitPerMinute: number;
  timeoutSeconds: number;
  activeSecretPrefix?: string | null;
  activeSecretVersion?: number;
  subscribedEvents: string[];
  createdAt: string;
  updatedAt?: string | null;
}

export interface EndpointCreatedResponse extends Endpoint {
  initialSecret: string;
  secretPrefix: string;
  secretVersion: number;
}

export interface WebhookSecret {
  id: string;
  endpointId: string;
  keyPrefix: string;
  version: number;
  status: SecretStatus;
  createdAt: string;
  revokedAt?: string | null;
}

export interface RotateSecretResponse {
  id: string;
  endpointId: string;
  newSecret: string;
  secretPrefix: string;
  version: number;
  status: SecretStatus;
  createdAt: string;
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

export interface TimeSeriesBucket {
  timestamp: string;
  total: number;
  success: number;
  failed: number;
  deadLettered: number;
  avgLatencyMs: number;
}

export interface DeliveryStats {
  totalDeliveries: number;
  successfulDeliveries: number;
  failedDeliveries: number;
  pendingDeliveries: number;
  deadLetteredDeliveries: number;
  successRatePercentage: number;
  averageLatencyMs: number;
  timeSeries?: TimeSeriesBucket[];
}

export interface PagedList<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface BulkReplayDeliveriesResponse {
  replayedCount: number;
  replayedDeliveries: Array<{
    deliveryId: string;
    originalDeliveryId: string;
    endpointId: string;
    eventType: string;
    status: DeliveryStatus;
  }>;
}
