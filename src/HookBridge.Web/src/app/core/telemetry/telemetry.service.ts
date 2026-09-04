import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class TelemetryService {
  /**
   * Generates a standard W3C TraceContext traceparent string: `00-{traceId}-{spanId}-01`
   */
  generateTraceParent(): string {
    const traceId = this.generateRandomHex(32);
    const spanId = this.generateRandomHex(16);
    return `00-${traceId}-${spanId}-01`;
  }

  /**
   * Generates a correlation ID with a descriptive prefix.
   */
  generateCorrelationId(prefix = 'portal'): string {
    return `${prefix}_${this.generateRandomHex(16)}`;
  }

  private generateRandomHex(length: number): string {
    const bytes = new Uint8Array(length / 2);
    crypto.getRandomValues(bytes);
    return Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('');
  }
}
