import { Injectable, inject, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel
} from '@microsoft/signalr';
import { AuthService } from '../../auth/services/auth.service';
import { HubConnectionStatus, RealtimeDeliveryEvent } from '../models/signalr.models';
import { environment } from '../../../../environments/environment';

const MAX_BUFFERED_EVENTS = 100;

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private readonly authService = inject(AuthService);
  private hubConnection: HubConnection | null = null;

  // Signal-based reactive state
  readonly status = signal<HubConnectionStatus>('disconnected');
  readonly latestEvent = signal<RealtimeDeliveryEvent | null>(null);
  readonly events = signal<RealtimeDeliveryEvent[]>([]);
  readonly subscribedEndpoints = signal<Set<string>>(new Set());

  async startConnection(): Promise<void> {
    if (this.hubConnection && this.hubConnection.state === HubConnectionState.Connected) {
      return;
    }

    const token = this.authService.getAccessToken();
    if (!token) {
      this.status.set('disconnected');
      return;
    }

    this.status.set('connecting');

    try {
      this.hubConnection = new HubConnectionBuilder()
        .withUrl(environment.signalrHubUrl, {
          accessTokenFactory: () => this.authService.getAccessToken() ?? ''
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(environment.production ? LogLevel.None : LogLevel.Information)
        .build();

      this.registerEventHandlers(this.hubConnection);

      await this.hubConnection.start();
      this.status.set('connected');
    } catch (err) {
      console.error('[SignalR] Connection error:', err);
      this.status.set('disconnected');
    }
  }

  async stopConnection(): Promise<void> {
    if (this.hubConnection) {
      try {
        await this.hubConnection.stop();
      } finally {
        this.hubConnection = null;
        this.status.set('disconnected');
      }
    }
  }

  async subscribeToEndpoint(endpointId: string): Promise<boolean> {
    if (!this.hubConnection || this.hubConnection.state !== HubConnectionState.Connected) {
      return false;
    }

    try {
      const success = await this.hubConnection.invoke<boolean>('SubscribeToEndpoint', endpointId);
      if (success) {
        this.subscribedEndpoints.update(prev => {
          const next = new Set(prev);
          next.add(endpointId);
          return next;
        });
      }
      return success;
    } catch (err) {
      console.error(`[SignalR] Failed to subscribe to endpoint ${endpointId}:`, err);
      return false;
    }
  }

  async unsubscribeFromEndpoint(endpointId: string): Promise<boolean> {
    if (!this.hubConnection || this.hubConnection.state !== HubConnectionState.Connected) {
      return false;
    }

    try {
      const success = await this.hubConnection.invoke<boolean>('UnsubscribeFromEndpoint', endpointId);
      if (success) {
        this.subscribedEndpoints.update(prev => {
          const next = new Set(prev);
          next.delete(endpointId);
          return next;
        });
      }
      return success;
    } catch (err) {
      console.error(`[SignalR] Failed to unsubscribe from endpoint ${endpointId}:`, err);
      return false;
    }
  }

  async subscribeToApplication(applicationId: string): Promise<boolean> {
    if (!this.hubConnection || this.hubConnection.state !== HubConnectionState.Connected) {
      return false;
    }

    try {
      return await this.hubConnection.invoke<boolean>('SubscribeToApplication', applicationId);
    } catch (err) {
      console.error(`[SignalR] Failed to subscribe to application ${applicationId}:`, err);
      return false;
    }
  }

  async unsubscribeFromApplication(applicationId: string): Promise<boolean> {
    if (!this.hubConnection || this.hubConnection.state !== HubConnectionState.Connected) {
      return false;
    }

    try {
      return await this.hubConnection.invoke<boolean>('UnsubscribeFromApplication', applicationId);
    } catch (err) {
      console.error(`[SignalR] Failed to unsubscribe from application ${applicationId}:`, err);
      return false;
    }
  }

  clearEvents(): void {
    this.events.set([]);
    this.latestEvent.set(null);
  }

  private registerEventHandlers(connection: HubConnection): void {
    connection.onreconnecting(() => {
      this.status.set('reconnecting');
    });

    connection.onreconnected(() => {
      this.status.set('connected');
    });

    connection.onclose(() => {
      this.status.set('disconnected');
    });

    connection.on('ReceiveDeliveryEvent', (event: RealtimeDeliveryEvent) => {
      this.processDeliveryEvent(event);
    });

    connection.on('DeliveryDispatched', (event: RealtimeDeliveryEvent) => {
      this.processDeliveryEvent(event);
    });

    connection.on('DeliveryAttemptRecorded', (event: RealtimeDeliveryEvent) => {
      this.processDeliveryEvent(event);
    });

    connection.on('DeliveryReplayed', (event: RealtimeDeliveryEvent) => {
      this.processDeliveryEvent(event);
    });

    connection.on('BulkDeliveriesReplayed', (events: RealtimeDeliveryEvent[]) => {
      for (const evt of events) {
        this.processDeliveryEvent(evt);
      }
    });
  }

  private processDeliveryEvent(event: RealtimeDeliveryEvent): void {
    this.latestEvent.set(event);
    this.events.update(current => {
      const updated = [event, ...current];
      return updated.slice(0, MAX_BUFFERED_EVENTS);
    });
  }
}
