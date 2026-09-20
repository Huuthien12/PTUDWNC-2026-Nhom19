import Link from "next/link";

interface CategoryPaginationProps {
  slug: string;
  currentPage: number;
  totalPages: number;
}

export default function CategoryPagination({
  slug,
  currentPage,
  totalPages,
}: CategoryPaginationProps) {
  if (totalPages <= 1) {
    return null;
  }

  const pages = Array.from(
    { length: totalPages },
    (_, index) => index + 1
  );

  return (
    <nav
      className="mt-10 flex flex-wrap justify-center gap-2"
      aria-label="Phân trang"
    >
      {currentPage > 1 && (
        <Link
          href={`/categories/${slug}?page=${currentPage - 1}`}
          className="rounded-lg border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
        >
          ← Trước
        </Link>
      )}

      {pages.map((page) => (
        <Link
          key={page}
          href={`/categories/${slug}?page=${page}`}
          className={
            page === currentPage
              ? "rounded-lg bg-orange-600 px-4 py-2 text-sm font-semibold text-white"
              : "rounded-lg border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
          }
        >
          {page}
        </Link>
      ))}

      {currentPage < totalPages && (
        <Link
          href={`/categories/${slug}?page=${currentPage + 1}`}
          className="rounded-lg border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
        >
          Sau →
        </Link>
      )}
    </nav>
  );
}