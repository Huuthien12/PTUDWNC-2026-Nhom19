import { apiClient } from "./api-client";
import type { UpdateProfileRequest, UserProfile } from "../types/profile";

export function getProfile(): Promise<UserProfile> {
  return apiClient<UserProfile>("/api/v1/auth/me", { auth: true });
}

export function updateProfile(request: UpdateProfileRequest): Promise<UserProfile> {
  const body = { fullName: request.fullName, ...(request.avatarUrl === undefined ? {} : { avatarUrl: request.avatarUrl }) };
  return apiClient<UserProfile>("/api/v1/auth/me", {
    auth: true,
    method: "PATCH",
    body: JSON.stringify(body),
  });
}
