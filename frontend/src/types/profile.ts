export interface UserProfile {
  id: string;
  fullName: string;
  email: string;
  userName: string;
  avatarUrl: string | null;
  roles: string[];
  emailConfirmed: boolean;
  createdAt: string;
}

export interface UpdateProfileRequest {
  fullName: string;
  avatarUrl?: string;
}
