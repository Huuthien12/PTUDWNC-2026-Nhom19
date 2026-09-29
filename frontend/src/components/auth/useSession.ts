"use client";

import { useSyncExternalStore } from "react";
import { getSession, subscribeSession } from "@/services/auth-session";

// undefined only while rendering on the server/hydrating; never expose browser tokens to RSC.
const serverSnapshot = () => undefined;
export function useSession() {
  return useSyncExternalStore(subscribeSession, getSession, serverSnapshot);
}
