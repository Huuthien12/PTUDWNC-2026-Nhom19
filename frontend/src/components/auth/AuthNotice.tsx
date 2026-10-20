import type { ReactNode } from "react";

type AuthNoticeProps = {
  kind: "error" | "success";
  children: ReactNode;
  className?: string;
  onDismiss?: () => void;
};

export default function AuthNotice({ kind, children, className = "", onDismiss }: AuthNoticeProps) {
  const isError = kind === "error";
  return (
    <div role={isError ? "alert" : "status"} aria-live={isError ? "assertive" : "polite"} className={`flex items-start gap-3 ${className}`}>
      <span aria-hidden="true" className="mt-0.5 inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-current/10 text-xs font-bold">{isError ? "!" : "✓"}</span>
      <div className="min-w-0 flex-1">{children}</div>
      {onDismiss && <button type="button" onClick={onDismiss} aria-label={isError ? "Đóng thông báo lỗi" : "Đóng thông báo thành công"} className="-mr-1 -mt-1 inline-flex h-7 w-7 shrink-0 items-center justify-center rounded transition hover:bg-black/5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-current"><span aria-hidden="true">×</span></button>}
    </div>
  );
}
