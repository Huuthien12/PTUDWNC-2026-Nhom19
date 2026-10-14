import RequireAuth from "@/components/auth/RequireAuth";
import ProfileForm from "@/components/auth/ProfileForm";

export default function ProfilePage() {
  return <RequireAuth><ProfileForm /></RequireAuth>;
}
