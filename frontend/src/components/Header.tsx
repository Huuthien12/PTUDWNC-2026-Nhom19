"use client";

import Link from "next/link";
import AuthControls from "@/components/auth/AuthControls";
import { usePathname } from "next/navigation";

export default function Header() {
  const pathname = usePathname();
  return <header className="cb-header">
    <div className="cb-container cb-header-inner">
      <Link href="/" className="cb-brand" aria-label="Culinary Blog — Trang chủ">Culinary <span>Blog</span><span className="text-gold" aria-hidden="true">.</span></Link>
      <nav className="cb-nav" aria-label="Điều hướng chính">
        <Link href="/" aria-current={pathname === "/" ? "page" : undefined}>Trang chủ</Link>
        <Link href="/categories" aria-current={pathname.startsWith("/categories") ? "page" : undefined}>Khám phá danh mục</Link>
        <AuthControls />
      </nav>
    </div>
  </header>;
}
