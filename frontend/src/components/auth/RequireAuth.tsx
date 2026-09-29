"use client";

import { useEffect, useState, type ReactNode } from "react";
import { usePathname, useRouter } from "next/navigation";
import { getValidSession } from "@/services/auth-session";
import { canAccess, loginDestination } from "@/services/auth-navigation";
import { useSession } from "./useSession";

export default function RequireAuth({ children, roles = [] }: { children: ReactNode; roles?: string[] }) {
  const session = useSession();
  const router = useRouter();
  const pathname = usePathname();
  const [checkedToken, setCheckedToken] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    if (session === undefined) return;
    if (!session) {
      router.replace(`/login?next=${encodeURIComponent(loginDestination(pathname))}`);
    } else {
      void getValidSession().then((current) => {
        if (active) setCheckedToken(current.accessToken);
      }).catch(() => {});
    }
    return () => { active = false; };
  }, [session, pathname, router]);

  if (!session || checkedToken !== session.accessToken) {
    return <p role="status" className="p-8 text-center">Đang kiểm tra phiên đăng nhập...</p>;
  }
  if (!canAccess(session.user.roles, roles)) {
    return <p role="alert" className="p-8 text-center">Bạn không có quyền truy cập trang này.</p>;
  }
  return children;
}
