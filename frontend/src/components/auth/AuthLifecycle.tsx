"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { getSession, getValidSession, SESSION_EXPIRED } from "@/services/auth-session";
import { useSession } from "./useSession";

export default function AuthLifecycle() {
  const router = useRouter();
  const session = useSession();

  useEffect(() => {
    const expired = () => router.replace("/login?reason=session-expired");
    window.addEventListener(SESSION_EXPIRED, expired);
    return () => window.removeEventListener(SESSION_EXPIRED, expired);
  }, [router]);

  useEffect(() => {
    const refreshIfExpired = () => {
      const current = getSession();
      if (current && Date.parse(current.expiresAt) <= Date.now()) {
        // The session service clears the session and emits the redirect event on failure.
        void getValidSession().catch(() => {});
      }
    };
    refreshIfExpired();
    const timer = session ? window.setTimeout(refreshIfExpired,
      Math.min(Math.max(Date.parse(session.expiresAt) - Date.now(), 0), 2147483647)) : undefined;
    window.addEventListener("focus", refreshIfExpired);
    document.addEventListener("visibilitychange", refreshIfExpired);
    return () => {
      window.clearTimeout(timer);
      window.removeEventListener("focus", refreshIfExpired);
      document.removeEventListener("visibilitychange", refreshIfExpired);
    };
  }, [session]);

  return null;
}
