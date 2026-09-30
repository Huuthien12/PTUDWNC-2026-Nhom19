import Link from "next/link";

export default function RecipePagination({ currentPage, totalPages }: { currentPage: number; totalPages: number }) {
  if (totalPages <= 1) return null;
  const pages = [...new Set([1, currentPage - 1, currentPage, currentPage + 1, totalPages])]
    .filter((page) => page >= 1 && page <= totalPages).sort((a, b) => a - b);
  const href = (page: number) => `/recipes?page=${page}`;

  return (
    <nav className="cb-pagination" aria-label="Phân trang công thức">
      {currentPage > 1 && <Link className="cb-button cb-button-secondary" href={href(currentPage - 1)}>← Trước</Link>}
      {pages.map((page, index) => (
        <span key={page} className="inline-flex items-center gap-2">
          {index > 0 && page - pages[index - 1] > 1 && <span className="px-2 text-muted" aria-hidden="true">…</span>}
          <Link href={href(page)} aria-current={page === currentPage ? "page" : undefined} aria-label={`Trang ${page}`} className={page === currentPage ? "cb-button" : "cb-button cb-button-secondary"}>{page}</Link>
        </span>
      ))}
      {currentPage < totalPages && <Link className="cb-button cb-button-secondary" href={href(currentPage + 1)}>Sau →</Link>}
    </nav>
  );
}
