export interface DocSectionDto {
  id: string;
  title: string;
  category: string;
  summary: string;
  contentMarkdown: string;
  codeSnippets: Record<string, string>;
}

export interface DocParamDto {
  name: string;
  type: string;
  required: boolean;
  description: string;
  example?: string;
}

export interface DocHeaderDto {
  name: string;
  required: boolean;
  description: string;
  example: string;
}

export interface DocEndpointDto {
  id: string;
  category: string;
  method: string;
  path: string;
  summary: string;
  description: string;
  pathParameters: DocParamDto[];
  queryParameters: DocParamDto[];
  requestHeaders: DocHeaderDto[];
  sampleRequestBody?: string;
  expectedStatusCode: number;
  sampleResponseBody?: string;
  codeSnippets: Record<string, string>;
}

export interface SdkRecipeDto {
  language: string;
  displayName: string;
  description: string;
  installationCommand: string;
  verificationSnippet: string;
  publishingSnippet: string;
  dependencies: string[];
}

export interface ApiReferenceResponse {
  version: string;
  environment: string;
  baseUrl: string;
  guides: DocSectionDto[];
  endpoints: DocEndpointDto[];
  sdkRecipes: SdkRecipeDto[];
}

export interface GenerateDocSnippetRequest {
  language: string;
  method: string;
  path: string;
  headers?: Record<string, string>;
  body?: string;
  apiKey?: string;
  signingSecret?: string;
}

export interface GenerateDocSnippetResponse {
  language: string;
  snippet: string;
  formattedCurl: string;
}

export interface SignatureVerificationResult {
  isValid: boolean;
  error?: string;
  timestamp?: number;
  clockSkewSeconds?: number;
  canonicalPayload?: string;
  computedSignature?: string;
}
