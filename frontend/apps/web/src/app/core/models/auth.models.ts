/**
 * Auth API contracts (mirror of backend Dtos/Auth). Tokens are never modeled here —
 * they live only in httpOnly cookies the browser cannot read.
 */

export interface AuthUser {
  id: string;
  email: string;
  emailConfirmed: boolean;
  /**
   * Server-derived (feature 004). Drives the first-login redirect into
   * `/onboarding`. UX-only; the server is the authority for the gate.
   */
  onboardingCompleted: boolean;
}

export interface RegisterRequest {
  email: string;
  password: string;
  /** Immutable, unique handle claimed at registration (feature 003). */
  handle: string;
}

export interface LoginRequest {
  email: string;
  password: string;
  rememberMe: boolean;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  userId: string;
  token: string;
  newPassword: string;
}

export interface ResendVerificationRequest {
  email: string;
}

export interface VerifyEmailRequest {
  userId: string;
  token: string;
}

export interface MessageResponse {
  message: string;
}

export interface VerificationRequiredResponse {
  status: 'email_not_verified';
  message: string;
}

export interface PasswordPolicy {
  minLength: number;
  requireDigit: boolean;
  requireLowercase: boolean;
  requireUppercase: boolean;
  requireNonAlphanumeric: boolean;
  requiredUniqueChars: number;
}
