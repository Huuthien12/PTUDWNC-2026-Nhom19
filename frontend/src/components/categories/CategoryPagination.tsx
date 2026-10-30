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

  const activePage = Math.min(Math.max(currentPage, 1), totalPages);
  const pages = [...new Set([
    1,
    activePage - 1,
    activePage,
    activePage + 1,
    totalPages,
  ])].filter(page => page >= 1 && page <= totalPages).sort((a, b) => a - b);

  return (
    <nav
      className="cb-pagination"
      aria-label="Phân trang"
    >
      {currentPage > 1 && (
        <Link
          href={`/categories/${encodeURIComponent(slug)}?page=${Math.min(currentPage - 1, totalPages)}`}
          className="cb-button cb-button-secondary"
        >
          ← Trước
        </Link>
      )}

      {pages.map((page, index) => (
        <span key={page} className="inline-flex items-center gap-2">
          {index > 0 && page - pages[index - 1] > 1 && (
            <span className="px-2 text-muted" aria-hidden="true">…</span>
          )}
          <Link
            aria-current={page === currentPage ? "page" : undefined}
            aria-label={`Trang ${page}`}
            href={`/categories/${encodeURIComponent(slug)}?page=${page}`}
            className={
              page === currentPage
                ? "cb-button"
                : "cb-button cb-button-secondary"
            }
          >
            {page}
          </Link>
        </span>
      ))}

      {currentPage < totalPages && (
        <Link
          href={`/categories/${encodeURIComponent(slug)}?page=${currentPage + 1}`}
          className="cb-button cb-button-secondary"
        >
          Sau →
        </Link>
      )}
    </nav>
  );
}
