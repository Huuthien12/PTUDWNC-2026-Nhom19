import Link from "next/link";

export default function Footer() {
  return <footer className="cb-footer">
    <div className="cb-container">
      <div className="cb-footer-grid">
        <div><Link href="/" className="cb-brand">Culinary <span>Blog.</span></Link>
          <p className="mt-5 max-w-sm text-sm leading-7">Một chút cảm hứng, một bữa cơm ngon.<br />Cùng khám phá những hương vị làm nên câu chuyện trong căn bếp của bạn.</p>
        </div>
        <div><h2>Khám phá</h2><ul className="space-y-3 text-sm"><li><Link href="/categories">Danh mục công thức</Link></li><li><Link href="/">Trang chủ</Link></li></ul></div>
        <div><h2>Góc của bạn</h2><ul className="space-y-3 text-sm"><li><Link href="/login">Đăng nhập</Link></li><li><Link href="/admin/categories">Quản lý danh mục</Link></li></ul></div>
      </div>
      <div className="cb-footer-bottom"><p>© Culinary Blog · Nhóm 19</p><p>Nấu ăn, sẻ chia &amp; tận hưởng.</p></div>
    </div>
  </footer>;
}
