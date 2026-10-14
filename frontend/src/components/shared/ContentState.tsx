import Link from "next/link";
import type { ReactNode } from "react";
import RetryButton from "./RetryButton";

export default function ContentState({ title, description, href, action, retry = false, retryAction }: {
  title: string; description: string; href?: string; action?: string; retry?: boolean; retryAction?: ReactNode;
}) {
  return <section className="cb-state">
    <h1 className="cb-section-title">{title}</h1>
    <p>{description}</p>
    {retryAction ?? (retry && <RetryButton />)}
    {href && action && <Link className="cb-button cb-button-secondary" href={href}>{action}</Link>}
  </section>;
}
