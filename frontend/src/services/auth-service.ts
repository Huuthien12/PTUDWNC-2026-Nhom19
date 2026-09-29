import { endSession, establishRegisteredSession, establishSession } from "./auth-session";
import type { AuthResponse } from "../types/auth";

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  fullName: string;
  email: string;
  userName: string;
  password: string;
}

export type LoginResponse = AuthResponse;

export async function login(
  request: LoginRequest
): Promise<LoginResponse> {
  return establishSession(request);
}

export const logout = (): Promise<void> => endSession();

export const register = (request: RegisterRequest): Promise<AuthResponse> =>
  establishRegisteredSession(request);
