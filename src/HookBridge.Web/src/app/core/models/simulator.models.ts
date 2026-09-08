export type SimulatorStrategy =
  | 'FixedStatus'
  | 'FailureRate'
  | 'SequentialRetryPattern'
  | 'Timeout'
  | 'ChaosJitter'
  | 'MalformedJson';

export interface SimulatorRule {
  id: string;
  tenantId: string;
  name: string;
  slug: string;
  receiverUrl: string;
  description?: string | null;
  strategy: SimulatorStrategy | number;
  strategyName: string;
  targetStatusCode: number;
  successStatusCode: number;
  failureRatePercent: number;
  failureStepCount: number;
  currentStepCount: number;
  delayMs: number;
  minDelayMs: number;
  maxDelayMs: number;
  responseHeadersJson?: string | null;
  responseBody?: string | null;
  responseContentType: string;
  isActive: boolean;
  totalExecutions: number;
  totalFailures: number;
  totalSuccesses: number;
  failureRateObservedPercent: number;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateSimulatorRuleRequest {
  name: string;
  slug?: string | null;
  description?: string | null;
  strategy: SimulatorStrategy | number;
  targetStatusCode: number;
  successStatusCode?: number;
  failureRatePercent?: number;
  failureStepCount?: number;
  delayMs?: number;
  minDelayMs?: number;
  maxDelayMs?: number;
  responseHeadersJson?: string | null;
  responseBody?: string | null;
  responseContentType?: string;
}

export interface UpdateSimulatorRuleRequest {
  name: string;
  description?: string | null;
  strategy: SimulatorStrategy | number;
  targetStatusCode: number;
  successStatusCode: number;
  failureRatePercent: number;
  failureStepCount: number;
  delayMs: number;
  minDelayMs: number;
  maxDelayMs: number;
  responseHeadersJson?: string | null;
  responseBody?: string | null;
  responseContentType: string;
  isActive: boolean;
}

export interface SimulatorExecution {
  id: string;
  ruleId?: string | null;
  ruleName?: string | null;
  httpMethod: string;
  path: string;
  queryString?: string | null;
  headersJson: string;
  body?: string | null;
  contentType?: string | null;
  contentLength: number;
  clientIp?: string | null;
  injectedFault: string;
  simulatedStatusCode: number;
  simulatedDelayMs: number;
  simulatedHeadersJson?: string | null;
  simulatedResponseBody?: string | null;
  executionDurationMs: number;
  executedAt: string;
}

export interface PagedSimulatorExecutions {
  items: SimulatorExecution[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface SimulatorStats {
  totalExecutions: number;
  totalFailures: number;
  totalSuccesses: number;
  overallFailureRatePercent: number;
  averageLatencyMs: number;
  activeRulesCount: number;
  statusDistribution: Record<string, number>;
  faultDistribution: Record<string, number>;
}

export interface TestDispatchRequest {
  ruleId?: string | null;
  adHocPreset?: string | null;
  httpMethod?: string;
  customUrl?: string | null;
  headersJson?: string | null;
  payloadBody?: string | null;
  adHocStatusCode?: number | null;
  adHocDelayMs?: number | null;
  adHocFailureRate?: number | null;
  adHocRetryAfter?: number | null;
}

export interface SimulatedExecutionResult {
  statusCode: number;
  delayMs: number;
  headers: Record<string, string>;
  body?: string | null;
  contentType: string;
  injectedFault: string;
  durationMs: number;
}

export interface RealtimeSimulatorEvent {
  eventType: 'SimulatorExecutionCaptured';
  tenantId: string;
  ruleId?: string | null;
  ruleSlug?: string | null;
  execution: SimulatorExecution;
  timestamp: string;
}
