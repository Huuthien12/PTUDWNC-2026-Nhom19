import RequireAuth from "@/components/auth/RequireAuth";

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return <RequireAuth roles={["Admin"]}>{children}</RequireAuth>;
}
