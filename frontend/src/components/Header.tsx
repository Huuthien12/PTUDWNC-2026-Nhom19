"use client";

import Link from "next/link";
import AuthControls from "@/components/auth/AuthControls";
import SearchBar from "@/components/search/SearchBar";
import { usePathname } from "next/navigation";
import { Suspense } from "react";

export default function Header() {
  const pathname = usePathname();
  if (pathname === "/login" || pathname === "/register") return null;

  return <header className="cb-header">
    <div className="cb-container cb-header-inner">
      <Link href="/" className="cb-brand shrink-0 whitespace-nowrap" aria-label="Culinary Blog — Trang chủ">Culinary <span>Blog</span><span className="text-gold" aria-hidden="true">.</span></Link>
      <div className="hidden min-w-0 flex-1 justify-center px-4 md:flex">
        <Suspense fallback={<span className="text-sm text-[#61695c]">Đang tải tìm kiếm...</span>}>
          <SearchBar compact />
        </Suspense>
      </div>
      <nav className="cb-nav min-w-0 justify-end" aria-label="Điều hướng chính">
        <Link href="/" aria-current={pathname === "/" ? "page" : undefined}>Trang chủ</Link>
        <Link href="/categories" aria-current={pathname.startsWith("/categories") ? "page" : undefined}>Khám phá danh mục</Link>
        <AuthControls />
      </nav>
    </div>
  </header>;
}
