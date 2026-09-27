"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

export default function Header() {
  const pathname = usePathname();
  return <header className="cb-header">
    <div className="cb-container cb-header-inner">
      <Link href="/" className="cb-brand" aria-label="Culinary Blog — Trang chủ">Culinary <span>Blog</span><span className="text-gold" aria-hidden="true">.</span></Link>
      <nav className="cb-nav" aria-label="Điều hướng chính">
        <Link href="/" aria-current={pathname === "/" ? "page" : undefined}>Trang chủ</Link>
        <Link href="/categories" aria-current={pathname.startsWith("/categories") ? "page" : undefined}>Khám phá danh mục</Link>
        <Link href="/login" aria-current={pathname === "/login" ? "page" : undefined}>Đăng nhập ↗</Link>
      </nav>
    </div>
  </header>;
}
