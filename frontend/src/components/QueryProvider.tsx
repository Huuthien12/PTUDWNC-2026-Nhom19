"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { getSession, subscribeSession } from "@/services/auth-session";

export default function QueryProvider({ children }: { children: React.ReactNode }) {
  const [client] = useState(() => new QueryClient());

  useEffect(() => {
    let sessionId = getSession()?.sessionId;
    return subscribeSession(() => {
      const next = getSession()?.sessionId;
      if (sessionId !== next) {
        sessionId = next;
        client.clear();
      }
    });
  }, [client]);

  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}
