import Link from "next/link";
import AuthControls from "@/components/auth/AuthControls";

export default function Header() {
  return (
    <header className="border-b bg-white">
      <div className="mx-auto flex min-h-16 max-w-7xl flex-wrap items-center justify-between gap-4 px-4 py-3">
        <Link href="/" className="text-xl font-bold">
          Culinary Blog
        </Link>

        <nav className="flex flex-wrap items-center gap-4">
          <Link
            href="/"
            className="text-sm font-medium hover:text-gray-600"
          >
            Trang chủ
          </Link>

          <Link
            href="/recipes"
            className="text-sm font-medium hover:text-gray-600"
          >
            Công thức
          </Link>

          <Link
            href="/categories"
            className="text-sm font-medium hover:text-gray-600"
          >
            Danh mục
          </Link>

          <Link
            href="/about"
            className="text-sm font-medium hover:text-gray-600"
          >
            Giới thiệu
          </Link>
        </nav>
        <AuthControls />
      </div>
    </header>
  );
}
