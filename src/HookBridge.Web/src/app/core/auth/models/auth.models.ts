export type UserRole = 'TenantAdmin' | 'Developer' | 'Viewer' | 'SystemOperator';
export type UserStatus = 'Active' | 'Suspended' | 'PendingVerification';

export interface UserProfile {
  userId: string;
  tenantId: string;
  tenantIdentifier: string;
  email: string;
  role: UserRole;
  status: UserStatus;
  lastLoginAt: string | null;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  tokenType: string;
  user: UserProfile;
}

export interface LoginCredentials {
  email: string;
  password: string;
  tenantIdentifier?: string | null;
}

export interface RegisterData {
  tenantIdentifier: string;
  tenantName: string;
  adminEmail: string;
  adminPassword: string;
}
