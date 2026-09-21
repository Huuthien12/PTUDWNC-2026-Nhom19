import { apiClient } from "./api-client";

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  tokenType: string;
}

export async function login(
  request: LoginRequest
): Promise<LoginResponse> {
  return apiClient<LoginResponse>("/api/v1/auth/login", {
    method: "POST",
    body: JSON.stringify(request),
  });
}