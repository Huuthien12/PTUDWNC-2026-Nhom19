export interface AuthUser {
  id: string;
  fullName: string;
  email: string;
  userName: string;
  avatarUrl: string | null;
  roles: string[];
}

export interface AuthResponse {
  accessToken: string;
  tokenType: string;
  refreshToken: string;
  expiresAt: string;
  user: AuthUser;
}

export interface AuthSession extends AuthResponse {
  // One login identity: late requests must not overwrite a newer login.
  sessionId: string;
}

export function isAuthResponse(value: unknown): value is AuthResponse {
  if (!value || typeof value !== "object") return false;
  const data = value as Partial<AuthResponse>;
  const user = data.user;
  return typeof data.accessToken === "string" && data.accessToken.length > 0 &&
    data.tokenType === "Bearer" && typeof data.refreshToken === "string" && data.refreshToken.length > 0 &&
    typeof data.expiresAt === "string" && Number.isFinite(Date.parse(data.expiresAt)) &&
    !!user && typeof user.id === "string" && typeof user.fullName === "string" &&
    typeof user.email === "string" && typeof user.userName === "string" &&
    (user.avatarUrl === null || typeof user.avatarUrl === "string") &&
    Array.isArray(user.roles) && user.roles.every((role) => typeof role === "string");
}
