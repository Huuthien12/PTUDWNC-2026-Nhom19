import CategoryList from "@/components/categories/CategoryList";
import CulinaryIllustration from "@/components/shared/CulinaryIllustration";
import ContentState from "@/components/shared/ContentState";
import { getCategories } from "@/services/category-service";
import type { Category } from "@/types/category";

export default async function CategoriesPage({ searchParams }: {
  searchParams: Promise<{ created?: string }>;
}) {
  const created = (await searchParams).created === "recipe";
  const notice = created ? <p role="status" className="cb-container pt-4 text-green-800">Đã lưu bản nháp công thức. Bản nháp chưa xuất hiện trong danh sách công khai.</p> : null;
  let categories: Category[];

  try {
    categories = await getCategories();
  } catch {
    return <div className="cb-container cb-section">{notice}<ContentState title="Chưa thể mở góc bếp" description="Danh mục tạm thời chưa tải được. Bạn hãy thử lại sau một chút nhé." retry /></div>;
  }

  return <>
    {notice}
    <section className="cb-hero"><div className="cb-container cb-hero-inner">
      <div><p className="cb-eyebrow">Cảm hứng từ căn bếp</p><h1 className="cb-title">Hôm nay,<br />mình nấu gì?</h1>
        <p className="cb-lead">Từ bữa cơm quen thuộc đến một hương vị mới. Chọn một danh mục và tìm cảm hứng cho lần vào bếp tiếp theo.</p>
        <a href="#danh-muc" className="cb-button mt-6">Khám phá danh mục <span aria-hidden="true">→</span></a>
      </div>
      <div className="cb-hero-art"><CulinaryIllustration /><p className="text-center text-xs tracking-widest uppercase">Chuyện ngon mỗi ngày</p></div>
    </div></section>
    <section id="danh-muc" className="cb-container cb-section" aria-labelledby="categories-heading">
      <div className="cb-section-heading"><h2 id="categories-heading" className="cb-section-title">Những góc hương vị</h2><span className="text-sm text-muted">{categories.length} danh mục để khám phá</span></div>
      <CategoryList categories={categories} />
    </section>
  </>;
}
