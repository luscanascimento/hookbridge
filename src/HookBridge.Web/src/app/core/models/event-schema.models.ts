export type SchemaCompatibilityMode = 'None' | 'Backward' | 'Forward' | 'Full';
export type SchemaStatus = 'Draft' | 'Active' | 'Deprecated' | 'Archived';

export interface EventSchemaSummary {
  id: string;
  eventType: string;
  name: string;
  description?: string;
  compatibilityMode: SchemaCompatibilityMode;
  status: SchemaStatus;
  totalVersions: number;
  activeVersion?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface EventSchemaVersion {
  id: string;
  eventSchemaId: string;
  version: string;
  versionNumber: number;
  schemaJson: string;
  description?: string;
  samplePayloadJson?: string;
  isActive: boolean;
  isDeprecated: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface EventSchemaDetail {
  id: string;
  eventType: string;
  name: string;
  description?: string;
  compatibilityMode: SchemaCompatibilityMode;
  status: SchemaStatus;
  versions: EventSchemaVersion[];
  activeVersion?: EventSchemaVersion;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateEventSchemaRequest {
  eventType: string;
  name: string;
  description?: string;
  compatibilityMode: SchemaCompatibilityMode;
  schemaJson: string;
  version?: string;
  versionDescription?: string;
  samplePayloadJson?: string;
}

export interface UpdateEventSchemaRequest {
  name: string;
  description?: string;
  compatibilityMode: SchemaCompatibilityMode;
  status?: SchemaStatus;
}

export interface CreateSchemaVersionRequest {
  version: string;
  schemaJson: string;
  description?: string;
  samplePayloadJson?: string;
  setActive?: boolean;
  forceOverrideCompatibility?: boolean;
}

export interface CheckCompatibilityRequest {
  oldSchemaJson?: string;
  newSchemaJson: string;
  mode: SchemaCompatibilityMode;
}

export interface CheckCompatibilityResponse {
  isCompatible: boolean;
  mode: SchemaCompatibilityMode;
  breakingChanges: string[];
  nonBreakingChanges: string[];
  warnings: string[];
}

export interface ValidateEventPayloadRequest {
  eventType?: string;
  schemaId?: string;
  versionId?: string;
  payloadJson: string;
}

export interface ValidateEventPayloadResponse {
  isValid: boolean;
  eventType: string;
  version: string;
  errors: string[];
  warnings: string[];
}

export interface SchemaDriftIssue {
  path: string;
  reason: string;
  severity: 'Error' | 'Warning' | string;
  sampleValue?: string;
}

export interface DetectSchemaDriftResponse {
  schemaId: string;
  eventType: string;
  activeVersion: string;
  deliveriesAnalyzed: number;
  conformingDeliveries: number;
  nonConformingDeliveries: number;
  conformanceRatePercentage: number;
  detectedDrifts: SchemaDriftIssue[];
}

export interface SchemaDocumentationResponse {
  eventType: string;
  schemaName: string;
  version: string;
  markdownDocs: string;
  typeScriptSnippet: string;
  cSharpSnippet: string;
  samplePayloadJson: string;
}
