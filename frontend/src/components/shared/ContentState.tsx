import Link from "next/link";
import type { ReactNode } from "react";
import RetryButton from "./RetryButton";

export default function ContentState({ title, description, href, action, retry = false, retryAction, headingAs: Heading = "h1" }: {
  title: string; description: string; href?: string; action?: string; retry?: boolean; retryAction?: ReactNode; headingAs?: "h1" | "h2";
}) {
  return <section className="cb-state">
    <Heading className="cb-section-title">{title}</Heading>
    <p>{description}</p>
    {retryAction ?? (retry && <RetryButton />)}
    {href && action && <Link className="cb-button cb-button-secondary" href={href}>{action}</Link>}
  </section>;
}
