import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  Delivery,
  DeliveryStats,
  DeliveryAttempt,
  PagedList,
  BulkReplayDeliveriesResponse
} from '../../shared/models/control-plane.models';
import { DeliveryStatus } from '../signalr/models/signalr.models';
import { environment } from '../../../environments/environment';

export interface DeliveryDetail extends Delivery {
  attempts: DeliveryAttempt[];
}

export interface DeliveryQueryParams {
  endpointId?: string | null;
  status?: DeliveryStatus | string | null;
  eventType?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
  correlationId?: string | null;
  page?: number;
  pageSize?: number;
}

@Injectable({
  providedIn: 'root'
})
export class DeliveryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/deliveries`;

  getStats(): Observable<DeliveryStats> {
    return this.http.get<DeliveryStats>(`${this.baseUrl}/stats`);
  }

  getDeliveries(params: DeliveryQueryParams = {}): Observable<PagedList<Delivery>> {
    let httpParams = new HttpParams();

    if (params.endpointId) httpParams = httpParams.set('endpointId', params.endpointId);
    if (params.status) httpParams = httpParams.set('status', params.status);
    if (params.eventType) httpParams = httpParams.set('eventType', params.eventType);
    if (params.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
    if (params.toDate) httpParams = httpParams.set('toDate', params.toDate);
    if (params.correlationId) httpParams = httpParams.set('correlationId', params.correlationId);
    if (params.page) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());

    return this.http.get<PagedList<Delivery>>(this.baseUrl, { params: httpParams });
  }

  getDeliveryById(id: string): Observable<DeliveryDetail> {
    return this.http.get<DeliveryDetail>(`${this.baseUrl}/${id}`);
  }

  replayDelivery(id: string, overrideEndpointId?: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/${id}/replay`, {
      overrideEndpointId: overrideEndpointId || null
    });
  }

  bulkReplay(command: {
    deliveryIds?: string[];
    endpointId?: string;
    status?: DeliveryStatus | string;
    eventType?: string;
    fromDate?: string;
    toDate?: string;
    maxCount?: number;
  }): Observable<BulkReplayDeliveriesResponse> {
    return this.http.post<BulkReplayDeliveriesResponse>(`${this.baseUrl}/replay`, command);
  }

  getLineage(id: string): Observable<{ rootDeliveryId: string; lineageChain: Delivery[] }> {
    return this.http.get<{ rootDeliveryId: string; lineageChain: Delivery[] }>(`${this.baseUrl}/${id}/lineage`);
  }
}
