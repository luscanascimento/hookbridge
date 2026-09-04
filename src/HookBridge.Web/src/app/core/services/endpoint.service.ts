import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  Endpoint,
  EndpointCreatedResponse,
  EndpointStatus,
  WebhookSecret,
  RotateSecretResponse,
  Application
} from '../../shared/models/control-plane.models';

export interface CreateEndpointRequest {
  applicationId: string;
  targetUrl: string;
  description?: string | null;
  rateLimitPerMinute?: number;
  timeoutSeconds?: number;
  subscribedEvents?: string[];
}

export interface UpdateEndpointRequest {
  targetUrl: string;
  description?: string | null;
  rateLimitPerMinute: number;
  timeoutSeconds: number;
}

@Injectable({
  providedIn: 'root'
})
export class EndpointService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/endpoints';
  private readonly appsUrl = '/api/v1/apps';

  getEndpoints(appId?: string): Observable<Endpoint[]> {
    let params = new HttpParams();
    if (appId) params = params.set('appId', appId);
    return this.http.get<Endpoint[]>(this.baseUrl, { params });
  }

  getEndpointById(id: string): Observable<Endpoint> {
    return this.http.get<Endpoint>(`${this.baseUrl}/${id}`);
  }

  createEndpoint(command: CreateEndpointRequest): Observable<EndpointCreatedResponse> {
    return this.http.post<EndpointCreatedResponse>(this.baseUrl, command);
  }

  updateEndpoint(id: string, command: UpdateEndpointRequest): Observable<Endpoint> {
    return this.http.put<Endpoint>(`${this.baseUrl}/${id}`, command);
  }

  updateStatus(id: string, status: EndpointStatus, reason?: string): Observable<Endpoint> {
    return this.http.patch<Endpoint>(`${this.baseUrl}/${id}/status`, { status, reason: reason ?? null });
  }

  deleteEndpoint(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  // Webhook Secrets
  getSecrets(endpointId: string): Observable<WebhookSecret[]> {
    return this.http.get<WebhookSecret[]>(`${this.baseUrl}/${endpointId}/secrets`);
  }

  rotateSecret(endpointId: string): Observable<RotateSecretResponse> {
    return this.http.post<RotateSecretResponse>(`${this.baseUrl}/${endpointId}/secrets/rotate`, {});
  }

  revokeSecret(endpointId: string, secretId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${endpointId}/secrets/${secretId}`);
  }

  // Applications
  getApplications(): Observable<Application[]> {
    return this.http.get<Application[]>(this.appsUrl);
  }

  createApplication(command: { name: string; description?: string }): Observable<Application> {
    return this.http.post<Application>(this.appsUrl, command);
  }
}
