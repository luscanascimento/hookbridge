import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  PayloadAnalysis,
  JsonPathEvaluation,
  JsonPathMatchItem,
  PayloadDiff,
  PayloadSchemaValidation
} from '../models/payload.models';

export interface SamplePayloadPreset {
  id: string;
  name: string;
  category: string;
  eventType: string;
  json: string;
}

@Injectable({
  providedIn: 'root'
})
export class PayloadService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/payloads`;

  analyzePayload(payloadJson: string): Observable<PayloadAnalysis> {
    return this.http.post<PayloadAnalysis>(`${this.baseUrl}/analyze`, { payloadJson });
  }

  evaluateJsonPath(payloadJson: string, jsonPath: string): Observable<JsonPathEvaluation> {
    return this.http.post<JsonPathEvaluation>(`${this.baseUrl}/jsonpath`, { payloadJson, jsonPath });
  }

  diffPayloads(leftJson: string, rightJson: string): Observable<PayloadDiff> {
    return this.http.post<PayloadDiff>(`${this.baseUrl}/diff`, { leftJson, rightJson });
  }

  validateSchema(payloadJson: string, schemaJson: string): Observable<PayloadSchemaValidation> {
    return this.http.post<PayloadSchemaValidation>(`${this.baseUrl}/validate`, { payloadJson, schemaJson });
  }

  formatJson(raw: string): string {
    if (!raw) return '';
    try {
      const parsed = typeof raw === 'string' ? JSON.parse(raw) : raw;
      return JSON.stringify(parsed, null, 2);
    } catch {
      return raw;
    }
  }

  minifyJson(raw: string): string {
    if (!raw) return '';
    try {
      const parsed = typeof raw === 'string' ? JSON.parse(raw) : raw;
      return JSON.stringify(parsed);
    } catch {
      return raw.replace(/\s+/g, ' ').trim();
    }
  }

  calculateByteSize(raw: string): number {
    if (!raw) return 0;
    return new TextEncoder().encode(raw).length;
  }

  getSamplePresets(): SamplePayloadPreset[] {
    return [
      {
        id: 'stripe-payment',
        name: 'Stripe — Payment Intent Succeeded',
        category: 'Fintech & Payments',
        eventType: 'payment_intent.succeeded',
        json: JSON.stringify({
          id: "evt_1Nt8Z82eZvKYlo2CLm4k5d1w",
          object: "event",
          api_version: "2024-06-20",
          created: 1725619200,
          type: "payment_intent.succeeded",
          data: {
            object: {
              id: "pi_3Nt8Z82eZvKYlo2C01234567",
              object: "payment_intent",
              amount: 12950,
              amount_received: 12950,
              currency: "usd",
              status: "succeeded",
              client_secret: "pi_3Nt8Z82eZvKYlo2C01234567_secret_xxxx",
              customer: "cus_PN87612345",
              payment_method: "pm_1Nt8Z72eZvKYlo2CKx67890",
              charges: {
                total_count: 1,
                data: [
                  {
                    id: "ch_3Nt8Z82eZvKYlo2C09876543",
                    amount: 12950,
                    paid: true,
                    receipt_url: "https://pay.stripe.com/receipts/acct_123/ch_987"
                  }
                ]
              },
              metadata: {
                order_id: "ord_99881",
                customer_tier: "enterprise"
              }
            }
          },
          livemode: true,
          pending_webhooks: 1
        }, null, 2)
      },
      {
        id: 'shopify-order',
        name: 'Shopify — Order Created & Paid',
        category: 'E-Commerce',
        eventType: 'orders/create',
        json: JSON.stringify({
          id: 593829104859,
          admin_graphql_api_id: "gid://shopify/Order/593829104859",
          order_number: 1042,
          financial_status: "paid",
          fulfillment_status: "unfulfilled",
          currency: "USD",
          total_price: "249.00",
          subtotal_price: "230.00",
          total_tax: "19.00",
          customer: {
            id: 7892345612,
            first_name: "Eleanor",
            last_name: "Vance",
            email: "eleanor.vance@company.com",
            orders_count: 4,
            verified_email: true
          },
          line_items: [
            {
              id: 140928301928,
              title: "Wireless Mechanical Keyboard (RGB)",
              price: "180.00",
              quantity: 1,
              sku: "KB-RGB-01",
              vendor: "KeyWorks"
            },
            {
              id: 140928301929,
              title: "Desk Mat (Midnight Navy)",
              price: "50.00",
              quantity: 1,
              sku: "MAT-NAVY-M",
              vendor: "DeskDesign"
            }
          ]
        }, null, 2)
      },
      {
        id: 'github-push',
        name: 'GitHub — Repository Push Event',
        category: 'DevOps & Git',
        eventType: 'push',
        json: JSON.stringify({
          ref: "refs/heads/main",
          before: "6ef9c35a812b1897c8d76b1f2e51923058914b10",
          after: "d788a8818c3d820461b3690d56bc0195e269ba1e",
          repository: {
            id: 92837482,
            name: "hookbridge",
            full_name: "luscanascimento/hookbridge",
            private: false,
            html_url: "https://github.com/luscanascimento/hookbridge"
          },
          pusher: {
            name: "luscanascimento",
            email: "lucas@hookbridge.io"
          },
          commits: [
            {
              id: "d788a8818c3d820461b3690d56bc0195e269ba1e",
              message: "feat: add trace explorer with distributed span waterfall",
              timestamp: "2026-09-06T10:15:30Z",
              author: {
                name: "Lucas Nascimento",
                email: "lucas@hookbridge.io"
              },
              added: ["src/HookBridge.Web/src/app/features/traces/trace-explorer.component.ts"],
              modified: ["docs/ROADMAP_PROGRESS.md"]
            }
          ]
        }, null, 2)
      },
      {
        id: 'hookbridge-incident',
        name: 'HookBridge — Delivery Attempt Failed (DLQ)',
        category: 'HookBridge Diagnostics',
        eventType: 'delivery.attempt.failed',
        json: JSON.stringify({
          deliveryId: "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          eventId: "7bb9a182-10f4-411a-821b-4191c9444102",
          endpointId: "e932b123-6543-4321-abcd-123456789abc",
          endpointUrl: "https://customer-api.production.internal/webhooks/billing",
          status: "DeadLettered",
          attemptNumber: 5,
          httpStatusCode: 504,
          elapsedMs: 5002,
          errorMessage: "Gateway Timeout: Upstream destination did not respond within configured 5000ms threshold.",
          traceParent: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
          correlationId: "corr_981240182",
          dispatchedAt: "2026-09-06T11:40:12.890Z"
        }, null, 2)
      }
    ];
  }
}
