"use client";

import { useRef, useState } from "react";

export function useRecipeMutation() {
  const locked = useRef(false);
  const stale = useRef(false);
  const [pending, setPending] = useState(false);
  const [needsReload, setNeedsReload] = useState(false);
  return {
    pending, needsReload,
    begin(reload = false) {
      if (locked.current || (stale.current && !reload)) return false;
      locked.current = true;
      setPending(true);
      return true;
    },
    end() { locked.current = false; setPending(false); },
    invalidate() { stale.current = true; setNeedsReload(true); },
    recover() { stale.current = false; setNeedsReload(false); },
    isBlocked() { return locked.current || stale.current; },
  };
}

export type RecipeMutationLock = ReturnType<typeof useRecipeMutation>;
