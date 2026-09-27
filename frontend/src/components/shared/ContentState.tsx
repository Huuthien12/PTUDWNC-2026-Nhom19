import Link from "next/link";
import RetryButton from "./RetryButton";

export default function ContentState({ title, description, href, action, retry = false }: {
  title: string; description: string; href?: string; action?: string; retry?: boolean;
}) {
  return <section className="cb-state">
    <h1 className="cb-section-title">{title}</h1>
    <p>{description}</p>
    {retry && <RetryButton />}
    {href && action && <Link className="cb-button cb-button-secondary" href={href}>{action}</Link>}
  </section>;
}
