import StaticUserList from "@/components/admin/StaticUserList";
import RequireAuth from "@/components/auth/RequireAuth";

export default function AdminUsersPage() {
  return <RequireAuth roles={["Admin"]}><main className="cb-container cb-section"><StaticUserList /></main></RequireAuth>;
}
