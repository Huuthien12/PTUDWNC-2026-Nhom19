import Link from "next/link";

export default function Header() {
  return (
    <header className="border-b bg-white">
      <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4">
        <Link href="/" className="text-xl font-bold">
          Culinary Blog
        </Link>

        <nav className="flex items-center gap-6">
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
      </div>
    </header>
  );
}