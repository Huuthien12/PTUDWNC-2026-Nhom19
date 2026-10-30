"use client";

import Link from "next/link";

export default function RootError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  void error;
  return <main className="cb-container cb-section">
    <section className="cb-state" role="alert">
      <h1 className="cb-section-title">Đã xảy ra sự cố</h1>
      <p>Không thể tải trang vào lúc này. Vui lòng thử lại hoặc quay về trang chủ.</p>
      <div className="mt-4 flex flex-wrap justify-center gap-3">
        <button type="button" className="cb-button" onClick={reset}>Thử lại</button>
        <Link className="cb-button cb-button-secondary" href="/">Về trang chủ</Link>
      </div>
    </section>
  </main>;
}
