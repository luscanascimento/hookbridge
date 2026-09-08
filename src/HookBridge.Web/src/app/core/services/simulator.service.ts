import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  SimulatorRule,
  CreateSimulatorRuleRequest,
  UpdateSimulatorRuleRequest,
  PagedSimulatorExecutions,
  SimulatorExecution,
  SimulatorStats,
  TestDispatchRequest,
  SimulatedExecutionResult
} from '../models/simulator.models';

@Injectable({
  providedIn: 'root'
})
export class SimulatorService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/simulator';

  getRules(): Observable<SimulatorRule[]> {
    return this.http.get<SimulatorRule[]>(`${this.baseUrl}/rules`);
  }

  getRuleById(id: string): Observable<SimulatorRule> {
    return this.http.get<SimulatorRule>(`${this.baseUrl}/rules/${id}`);
  }

  createRule(request: CreateSimulatorRuleRequest): Observable<SimulatorRule> {
    return this.http.post<SimulatorRule>(`${this.baseUrl}/rules`, request);
  }

  updateRule(id: string, request: UpdateSimulatorRuleRequest): Observable<SimulatorRule> {
    return this.http.put<SimulatorRule>(`${this.baseUrl}/rules/${id}`, request);
  }

  deleteRule(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/rules/${id}`);
  }

  resetRuleSteps(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/rules/${id}/reset`, {});
  }

  getExecutions(filter?: {
    ruleId?: string;
    statusCode?: number;
    method?: string;
    search?: string;
    page?: number;
    pageSize?: number;
  }): Observable<PagedSimulatorExecutions> {
    let params = new HttpParams();
    if (filter?.ruleId) params = params.set('ruleId', filter.ruleId);
    if (filter?.statusCode) params = params.set('statusCode', filter.statusCode.toString());
    if (filter?.method) params = params.set('method', filter.method);
    if (filter?.search) params = params.set('search', filter.search);
    if (filter?.page) params = params.set('page', filter.page.toString());
    if (filter?.pageSize) params = params.set('pageSize', filter.pageSize.toString());

    return this.http.get<PagedSimulatorExecutions>(`${this.baseUrl}/executions`, { params });
  }

  getExecutionById(id: string): Observable<SimulatorExecution> {
    return this.http.get<SimulatorExecution>(`${this.baseUrl}/executions/${id}`);
  }

  clearExecutions(ruleId?: string): Observable<void> {
    let params = new HttpParams();
    if (ruleId) params = params.set('ruleId', ruleId);
    return this.http.delete<void>(`${this.baseUrl}/executions`, { params });
  }

  getStats(): Observable<SimulatorStats> {
    return this.http.get<SimulatorStats>(`${this.baseUrl}/stats`);
  }

  testDispatch(request: TestDispatchRequest): Observable<SimulatedExecutionResult> {
    return this.http.post<SimulatedExecutionResult>(`${this.baseUrl}/test-dispatch`, request);
  }
}
