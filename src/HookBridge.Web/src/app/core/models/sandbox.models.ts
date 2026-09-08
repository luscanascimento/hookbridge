export interface WebhookSandbox {
  id: string;
  name: string;
  slug: string;
  receiverUrl: string;
  defaultResponseStatusCode: number;
  defaultResponseBody?: string | null;
  defaultResponseContentType: string;
  defaultResponseDelayMs: number;
  isActive: boolean;
  expiresAt?: string | null;
  totalRequestsCount: number;
  lastRequestAt?: string | null;
  createdAt: string;
}

export interface CreateSandboxRequest {
  name: string;
  customSlug?: string | null;
  defaultStatusCode?: number;
  defaultBody?: string | null;
  defaultContentType?: string;
  defaultDelayMs?: number;
  ttlHours?: number | null;
}

export interface UpdateSandboxConfigRequest {
  name: string;
  defaultStatusCode: number;
  defaultBody?: string | null;
  defaultContentType: string;
  defaultDelayMs: number;
  isActive: boolean;
}

export interface SandboxRequestSummary {
  id: string;
  sandboxId: string;
  httpMethod: string;
  path: string;
  contentType?: string | null;
  contentLength: number;
  clientIp?: string | null;
  responseStatusCode: number;
  responseDelayMs: number;
  receivedAt: string;
  durationMs: number;
}

export interface SandboxRequestDetail {
  id: string;
  sandboxId: string;
  httpMethod: string;
  path: string;
  queryString?: string | null;
  headersJson: string;
  body?: string | null;
  contentType?: string | null;
  contentLength: number;
  clientIp?: string | null;
  responseStatusCode: number;
  responseDelayMs: number;
  receivedAt: string;
  durationMs: number;
}

export interface PagedSandboxRequests {
  items: SandboxRequestSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface RealtimeSandboxEvent {
  eventType: 'SandboxRequestCaptured';
  tenantId: string;
  sandboxId: string;
  sandboxSlug: string;
  request: SandboxRequestDetail;
  timestamp: string;
}
