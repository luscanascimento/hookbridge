import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  WebhookSandbox,
  CreateSandboxRequest,
  UpdateSandboxConfigRequest,
  PagedSandboxRequests,
  SandboxRequestDetail
} from '../models/sandbox.models';

@Injectable({
  providedIn: 'root'
})
export class SandboxService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/sandboxes';

  getSandboxes(): Observable<WebhookSandbox[]> {
    return this.http.get<WebhookSandbox[]>(this.baseUrl);
  }

  getSandboxById(id: string): Observable<WebhookSandbox> {
    return this.http.get<WebhookSandbox>(`${this.baseUrl}/${id}`);
  }

  createSandbox(request: CreateSandboxRequest): Observable<WebhookSandbox> {
    return this.http.post<WebhookSandbox>(this.baseUrl, request);
  }

  updateSandbox(id: string, request: UpdateSandboxConfigRequest): Observable<WebhookSandbox> {
    return this.http.put<WebhookSandbox>(`${this.baseUrl}/${id}`, request);
  }

  deleteSandbox(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  getSandboxRequests(
    sandboxId: string,
    filter?: {
      method?: string;
      search?: string;
      statusCode?: number;
      page?: number;
      pageSize?: number;
    }
  ): Observable<PagedSandboxRequests> {
    let params = new HttpParams();
    if (filter?.method) params = params.set('method', filter.method);
    if (filter?.search) params = params.set('search', filter.search);
    if (filter?.statusCode) params = params.set('statusCode', filter.statusCode.toString());
    if (filter?.page) params = params.set('page', filter.page.toString());
    if (filter?.pageSize) params = params.set('pageSize', filter.pageSize.toString());

    return this.http.get<PagedSandboxRequests>(`${this.baseUrl}/${sandboxId}/requests`, { params });
  }

  getSandboxRequestById(sandboxId: string, requestId: string): Observable<SandboxRequestDetail> {
    return this.http.get<SandboxRequestDetail>(`${this.baseUrl}/${sandboxId}/requests/${requestId}`);
  }

  clearSandboxRequests(sandboxId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${sandboxId}/requests`);
  }
}
