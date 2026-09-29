"use client";

import { useRouter } from "next/navigation";
import { useTransition } from "react";

export default function RetryButton() {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();

  return (
    <button
      type="button"
      className="cb-button cb-button-secondary"
      disabled={isPending}
      aria-busy={isPending}
      onClick={() => startTransition(() => router.refresh())}
    >
      {isPending ? "Đang tải lại…" : "Thử tải lại"}
    </button>
  );
}
