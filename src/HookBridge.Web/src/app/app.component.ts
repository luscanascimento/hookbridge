import { Component, effect, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth/services/auth.service';
import { SignalRService } from './core/signalr/services/signalr.service';

import { ToastContainerComponent } from './shared/components/ui/toast/toast-container.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, ToastContainerComponent],
  template: `
    <router-outlet></router-outlet>
    <app-toast-container></app-toast-container>
  `
})
export class AppComponent {
  private readonly auth = inject(AuthService);
  private readonly signalR = inject(SignalRService);

  constructor() {
    // Automatically manage SignalR connection whenever authentication state transitions
    effect(() => {
      const isAuth = this.auth.isAuthenticated();
      if (isAuth) {
        this.signalR.startConnection();
      } else {
        this.signalR.stopConnection();
      }
    });
  }
}
