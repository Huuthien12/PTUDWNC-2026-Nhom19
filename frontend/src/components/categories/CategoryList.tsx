import CategoryCard from "./CategoryCard";
import type { Category } from "@/types/category";

export default function CategoryList({ categories }: { categories: Category[] }) {
  if (!categories.length) return <div className="cb-state"><h2 className="cb-section-title">Góc bếp đang được chuẩn bị</h2><p>Các danh mục mới sẽ sớm xuất hiện. Mời bạn ghé lại sau.</p></div>;
  return <div className="cb-grid">{categories.map(category => <CategoryCard key={category.id} category={category} />)}</div>;
}
