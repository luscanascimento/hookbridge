import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ApiReferenceResponse,
  GenerateDocSnippetRequest,
  GenerateDocSnippetResponse,
  SdkRecipeDto
} from '../models/doc.models';

@Injectable({
  providedIn: 'root'
})
export class DocService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/docs';

  getApiReference(customBaseUrl?: string, apiKey?: string): Observable<ApiReferenceResponse> {
    let params = new HttpParams();
    if (customBaseUrl) {
      params = params.set('baseUrl', customBaseUrl);
    }
    if (apiKey) {
      params = params.set('apiKey', apiKey);
    }
    return this.http.get<ApiReferenceResponse>(`${this.baseUrl}/reference`, { params });
  }

  generateSnippet(request: GenerateDocSnippetRequest): Observable<GenerateDocSnippetResponse> {
    return this.http.post<GenerateDocSnippetResponse>(`${this.baseUrl}/snippets`, request);
  }

  getSdkRecipe(language: string, customBaseUrl?: string, apiKey?: string): Observable<SdkRecipeDto> {
    let params = new HttpParams();
    if (customBaseUrl) {
      params = params.set('baseUrl', customBaseUrl);
    }
    if (apiKey) {
      params = params.set('apiKey', apiKey);
    }
    return this.http.get<SdkRecipeDto>(`${this.baseUrl}/sdk-recipes/${encodeURIComponent(language)}`, { params });
  }

  verifySignatureInteractive(secret: string, signatureHeader: string, rawPayload: string, toleranceSeconds = 300): Observable<{
    isValid: boolean;
    parsedTimestamp?: number;
    clockSkewSeconds?: number;
    matchedSignatureVersion?: string;
  }> {
    return this.http.post<{
      isValid: boolean;
      parsedTimestamp?: number;
      clockSkewSeconds?: number;
      matchedSignatureVersion?: string;
    }>('/api/v1/webhook-signatures/verify', {
      secret,
      signatureHeader,
      rawPayload,
      toleranceSeconds
    });
  }
}
