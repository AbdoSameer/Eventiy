import { UserRole } from '../enums/user-role.enum';

export type { UserRole };

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  role: UserRole;
}

export interface AuthResponse {
  userId: string;
  email: string;
  role: UserRole;
  token: string | null;
  expiresAt: string | null;
  requiresApproval: boolean;
}
