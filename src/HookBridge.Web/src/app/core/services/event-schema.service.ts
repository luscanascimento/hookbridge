import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  EventSchemaSummary,
  EventSchemaDetail,
  EventSchemaVersion,
  CreateEventSchemaRequest,
  UpdateEventSchemaRequest,
  CreateSchemaVersionRequest,
  CheckCompatibilityRequest,
  CheckCompatibilityResponse,
  ValidateEventPayloadRequest,
  ValidateEventPayloadResponse,
  DetectSchemaDriftResponse,
  SchemaDocumentationResponse,
  SchemaStatus,
  SchemaCompatibilityMode
} from '../models/event-schema.models';

@Injectable({
  providedIn: 'root'
})
export class EventSchemaService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/schemas';

  getSchemas(
    search?: string,
    status?: SchemaStatus,
    compatibilityMode?: SchemaCompatibilityMode
  ): Observable<EventSchemaSummary[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    if (status) params = params.set('status', status);
    if (compatibilityMode) params = params.set('compatibilityMode', compatibilityMode);

    return this.http.get<EventSchemaSummary[]>(this.baseUrl, { params });
  }

  getSchemaById(id: string): Observable<EventSchemaDetail> {
    return this.http.get<EventSchemaDetail>(`${this.baseUrl}/${id}`);
  }

  createSchema(request: CreateEventSchemaRequest): Observable<EventSchemaDetail> {
    return this.http.post<EventSchemaDetail>(this.baseUrl, request);
  }

  updateSchema(id: string, request: UpdateEventSchemaRequest): Observable<EventSchemaDetail> {
    return this.http.put<EventSchemaDetail>(`${this.baseUrl}/${id}`, request);
  }

  deleteSchema(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  createVersion(schemaId: string, request: CreateSchemaVersionRequest): Observable<EventSchemaVersion> {
    return this.http.post<EventSchemaVersion>(`${this.baseUrl}/${schemaId}/versions`, request);
  }

  activateVersion(schemaId: string, versionId: string): Observable<EventSchemaVersion> {
    return this.http.post<EventSchemaVersion>(`${this.baseUrl}/${schemaId}/versions/${versionId}/activate`, {});
  }

  deprecateVersion(schemaId: string, versionId: string): Observable<EventSchemaVersion> {
    return this.http.post<EventSchemaVersion>(`${this.baseUrl}/${schemaId}/versions/${versionId}/deprecate`, {});
  }

  checkCompatibility(request: CheckCompatibilityRequest): Observable<CheckCompatibilityResponse> {
    return this.http.post<CheckCompatibilityResponse>(`${this.baseUrl}/compatibility/check`, request);
  }

  validatePayload(request: ValidateEventPayloadRequest): Observable<ValidateEventPayloadResponse> {
    return this.http.post<ValidateEventPayloadResponse>(`${this.baseUrl}/validate`, request);
  }

  detectDrift(schemaId: string, sampleLimit = 50): Observable<DetectSchemaDriftResponse> {
    return this.http.post<DetectSchemaDriftResponse>(`${this.baseUrl}/${schemaId}/drift`, { schemaId, sampleLimit });
  }

  getSchemaDocs(schemaId: string, versionId?: string): Observable<SchemaDocumentationResponse> {
    let params = new HttpParams();
    if (versionId) params = params.set('versionId', versionId);
    return this.http.get<SchemaDocumentationResponse>(`${this.baseUrl}/${schemaId}/docs`, { params });
  }
}
