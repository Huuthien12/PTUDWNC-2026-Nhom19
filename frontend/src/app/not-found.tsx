import Link from "next/link";

export default function NotFound() {
  return <main className="cb-container cb-section">
    <section className="cb-state">
      <p className="text-sm font-semibold uppercase tracking-[0.18em] text-[#254b3b]">404</p>
      <h1 className="cb-section-title">Không tìm thấy trang</h1>
      <p>Đường dẫn này không còn tồn tại hoặc chưa được tạo.</p>
      <Link className="cb-button cb-button-secondary" href="/">Về trang chủ</Link>
    </section>
  </main>;
}
