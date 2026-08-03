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
  /**
   * The signed-in user's immutable handle / profile slug (feature 026). Powers the
   * link to their own profile (`/u/<handle>`) and owner detection on the profile page.
   */
  handle: string;
  /**
   * The user's chosen interface language (feature 031): `"en" | "de" | "es"`, or `null` when they
   * haven't chosen one (the client then resolves a language by local/browser detection). Top of the
   * language precedence when set.
   */
  preferredLanguage: string | null;
}

export interface RegisterRequest {
  email: string;
  password: string;
  /** Immutable, unique handle claimed at registration (feature 003). */
  handle: string;

  /** The reader ticked the box themselves (feature 041). Never defaulted to true. */
  acceptsTerms: boolean;

  /**
   * The Terms of Use version this client actually displayed, read from the legal catalogue —
   * never hard-coded here. The server refuses anything that is not the current version, which is
   * what proves the reader saw the current text rather than a stale cached copy.
   */
  termsVersion: string;

  /** The language the document was shown in, recorded alongside the acceptance. */
  termsLanguage: string;
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
