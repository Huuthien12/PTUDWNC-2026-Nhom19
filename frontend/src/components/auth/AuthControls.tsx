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
      {session.user.roles.some((role) => role === "Author" || role === "Admin") && <>
        <Link href="/recipes/dashboard">Công thức của tôi</Link>
        <Link href="/recipes/create">Tạo công thức</Link>
      </>}
      {session.user.roles.includes("Admin") && <Link href="/admin/categories">Quản trị</Link>}
      <details className="group relative">
        <summary className="list-none cursor-pointer rounded-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#d7b354] [&::-webkit-details-marker]:hidden" aria-label="Mở menu tài khoản">
          {session.user.avatarUrl ? (
            <img src={session.user.avatarUrl} alt="Ảnh đại diện" referrerPolicy="no-referrer" className="h-10 w-10 rounded-full border-2 border-white object-cover shadow-sm" />
          ) : (
            <span aria-hidden="true" className="flex h-10 w-10 items-center justify-center rounded-full border-2 border-white bg-[#91aa82] text-xs font-bold text-[#254b3b] shadow-sm">
              {(session.user.fullName || session.user.userName).trim().split(/\s+/).slice(-2).map(part => part[0]).join("").toLocaleUpperCase("vi-VN") || "CB"}
            </span>
          )}
        </summary>
        <div className="absolute right-0 z-50 mt-2 w-44 overflow-hidden rounded-xl border border-[#d8d9cb] bg-white py-1 text-sm text-[#253329] shadow-lg">
          <Link href="/profile" className="block px-4 py-3 hover:bg-[#f1f3ed]">Hồ sơ</Link>
          <button type="button" onClick={() => void handleLogout()} disabled={pending}
            className="block w-full border-t border-[#ecece5] px-4 py-3 text-left text-red-600 hover:bg-red-50 disabled:opacity-50">
            {pending ? "Đang đăng xuất..." : "Đăng xuất"}
          </button>
        </div>
      </details>
    </div>
  );
}
