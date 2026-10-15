import RequireAuth from "@/components/auth/RequireAuth";
import ChangePasswordForm from "@/components/auth/ChangePasswordForm";

export default function ChangePasswordPage() {
  return <RequireAuth><ChangePasswordForm /></RequireAuth>;
}
