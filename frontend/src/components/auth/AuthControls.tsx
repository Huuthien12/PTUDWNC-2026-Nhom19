"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { logout } from "@/services/auth-service";
import { useSession } from "./useSession";

export default function AuthControls() {
  const session = useSession();
  const router = useRouter();
  const [pending, setPending] = useState(false);

  async function handleLogout() {
    setPending(true);
    try {
      await logout();
      router.replace("/login");
    } catch {
      router.replace("/login?reason=logout-unconfirmed");
    } finally {
      setPending(false);
    }
  }

  if (session === undefined) return <span className="text-sm">Đang tải...</span>;
  if (!session) return <Link href="/login" className="text-sm font-medium">Đăng nhập</Link>;
  return (
    <div className="flex flex-wrap items-center gap-3 text-sm">
      {session.user.roles.includes("Admin") && <Link href="/admin/categories">Quản trị</Link>}
      <span>{session.user.fullName || session.user.userName}</span>
      <button type="button" onClick={() => void handleLogout()} disabled={pending}
        className="rounded border px-3 py-1 disabled:opacity-50">
        {pending ? "Đang đăng xuất..." : "Đăng xuất"}
      </button>
    </div>
  );
}
