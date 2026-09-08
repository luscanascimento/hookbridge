import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ObservabilitySummary,
  MetricInstrument,
  CapturedSpan,
  SyntheticTraceCommand,
  SyntheticTraceResult
} from '../models/observability.models';

@Injectable({
  providedIn: 'root'
})
export class ObservabilityService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/observability';

  getSummary(): Observable<ObservabilitySummary> {
    return this.http.get<ObservabilitySummary>(`${this.baseUrl}/summary`);
  }

  getInstruments(): Observable<MetricInstrument[]> {
    return this.http.get<MetricInstrument[]>(`${this.baseUrl}/instruments`);
  }

  getRecentSpans(count = 50): Observable<CapturedSpan[]> {
    const params = new HttpParams().set('count', count.toString());
    return this.http.get<CapturedSpan[]>(`${this.baseUrl}/spans`, { params });
  }

  generateSyntheticTrace(command: SyntheticTraceCommand): Observable<SyntheticTraceResult> {
    return this.http.post<SyntheticTraceResult>(`${this.baseUrl}/synthetic-trace`, command);
  }

  getPrometheusMetrics(): Observable<string> {
    return this.http.get('/metrics', { responseType: 'text' });
  }
}
