import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Endpoint } from '../../shared/models/control-plane.models';

@Injectable({
  providedIn: 'root'
})
export class EndpointService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/endpoints';

  getEndpoints(appId?: string): Observable<Endpoint[]> {
    let params = new HttpParams();
    if (appId) params = params.set('appId', appId);
    return this.http.get<Endpoint[]>(this.baseUrl, { params });
  }

  getEndpointById(id: string): Observable<Endpoint> {
    return this.http.get<Endpoint>(`${this.baseUrl}/${id}`);
  }

  createEndpoint(command: {
    applicationId: string;
    targetUrl: string;
    description?: string | null;
    rateLimitPerMinute?: number;
    timeoutSeconds?: number;
    eventPatterns: string[];
  }): Observable<any> {
    return this.http.post(this.baseUrl, command);
  }

  updateEndpoint(id: string, command: {
    targetUrl: string;
    description?: string | null;
    rateLimitPerMinute: number;
    timeoutSeconds: number;
  }): Observable<Endpoint> {
    return this.http.put<Endpoint>(`${this.baseUrl}/${id}`, command);
  }

  updateStatus(id: string, status: 'Active' | 'Paused' | 'Disabled'): Observable<Endpoint> {
    return this.http.patch<Endpoint>(`${this.baseUrl}/${id}/status`, { status });
  }

  deleteEndpoint(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
