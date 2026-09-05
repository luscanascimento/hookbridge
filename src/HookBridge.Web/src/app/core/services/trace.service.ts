import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PagedList } from '../../shared/models/control-plane.models';
import { TraceSummary, TraceDetail, TraceQueryParams } from '../../shared/models/trace.models';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class TraceService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/traces`;

  getTraces(params: TraceQueryParams = {}): Observable<PagedList<TraceSummary>> {
    let httpParams = new HttpParams();

    if (params.query) httpParams = httpParams.set('query', params.query);
    if (params.status) httpParams = httpParams.set('status', params.status);
    if (params.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
    if (params.toDate) httpParams = httpParams.set('toDate', params.toDate);
    if (params.page) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());

    return this.http.get<PagedList<TraceSummary>>(this.baseUrl, { params: httpParams });
  }

  getTraceDetail(identifier: string): Observable<TraceDetail> {
    return this.http.get<TraceDetail>(`${this.baseUrl}/${encodeURIComponent(identifier)}`);
  }
}
