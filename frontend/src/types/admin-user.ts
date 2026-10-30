import type { PaginatedResult } from "./api";

export interface AdminUser {
  id: string;
  fullName: string;
  email: string;
  userName: string;
  roles: string[];
  emailConfirmed: boolean;
  createdAt: string;
}

export type AdminUserPage = PaginatedResult<AdminUser>;
