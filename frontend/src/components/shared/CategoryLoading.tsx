export default function CategoryLoading() {
  return <section className="cb-container cb-section" aria-busy="true" aria-label="Đang tải danh mục">
    <p role="status" className="cb-eyebrow mb-8">Đang mở góc bếp…</p>
    <div className="cb-grid" aria-hidden="true">{[0, 1, 2, 3, 4, 5].map(i => <div key={i} className="cb-skeleton" />)}</div>
  </section>;
}
