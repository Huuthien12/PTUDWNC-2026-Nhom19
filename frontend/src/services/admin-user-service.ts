import { apiClient } from "./api-client";
import type { AdminUserPage } from "@/types/admin-user";

export function getAdminUsers(page = 1, pageSize = 12, search = "", role = ""): Promise<AdminUserPage> {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (search.trim()) query.set("search", search.trim());
  if (role) query.set("role", role);
  return apiClient<AdminUserPage>(`/api/v1/admin/users?${query}`, { auth: true, cache: "no-store" });
}
