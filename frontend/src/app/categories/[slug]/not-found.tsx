import ContentState from "@/components/shared/ContentState";
export default function CategoryNotFound() {
  return <div className="cb-container cb-section"><ContentState title="Chưa tìm thấy danh mục" description="Danh mục này không còn tồn tại hoặc đường dẫn chưa đúng. Hãy khám phá một góc bếp khác nhé." href="/categories" action="Xem tất cả danh mục" /></div>;
}
