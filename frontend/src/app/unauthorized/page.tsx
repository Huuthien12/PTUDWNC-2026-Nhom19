import Link from "next/link";

export default function UnauthorizedPage() {
  return <main className="cb-container cb-section">
    <section className="cb-state" role="alert">
      <p className="text-sm font-semibold uppercase tracking-[0.18em] text-[#254b3b]">403</p>
      <h1 className="cb-section-title">Bạn không có quyền truy cập</h1>
      <p>Trang này yêu cầu quyền truy cập khác với tài khoản hiện tại của bạn.</p>
      <Link className="cb-button cb-button-secondary" href="/">Về trang chủ</Link>
    </section>
  </main>;
}
