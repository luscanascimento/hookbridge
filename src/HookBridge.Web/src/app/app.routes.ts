import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/guards/auth.guard';
import { AppShellComponent } from './shared/components/layout/app-shell.component';

export const routes: Routes = [
  {
    path: 'auth',
    canActivate: [guestGuard],
    children: [
      {
        path: 'login',
        loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent)
      },
      {
        path: 'register',
        loadComponent: () => import('./features/auth/register.component').then(m => m.RegisterComponent)
      },
      {
        path: '',
        redirectTo: 'login',
        pathMatch: 'full'
      }
    ]
  },
  {
    path: '',
    component: AppShellComponent,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'endpoints',
        loadComponent: () => import('./features/endpoints/endpoints.component').then(m => m.EndpointsComponent)
      },
      {
        path: 'deliveries',
        loadComponent: () => import('./features/deliveries/deliveries.component').then(m => m.DeliveriesComponent)
      },
      {
        path: 'live',
        loadComponent: () => import('./features/deliveries/deliveries.component').then(m => m.DeliveriesComponent)
      },
      {
        path: 'events',
        loadComponent: () => import('./features/deliveries/deliveries.component').then(m => m.DeliveriesComponent)
      },
      {
        path: '**',
        redirectTo: 'dashboard'
      }
    ]
  }
];
