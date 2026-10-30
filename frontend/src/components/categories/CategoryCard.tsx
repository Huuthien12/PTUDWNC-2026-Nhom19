import Link from "next/link";
import CulinaryIllustration from "@/components/shared/CulinaryIllustration";
import type { Category } from "@/types/category";

export default function CategoryCard({ category }: { category: Category }) {
  return <Link href={`/categories/${category.slug}`} className="cb-category">
    <div className="cb-category-top"><div className="cb-category-art"><CulinaryIllustration /></div>
      <span className="text-xs text-muted">{category.recipeCount} công thức</span></div>
    <h2>{category.name}</h2>
    <p>{category.description?.trim() || "Một góc hương vị để bạn khám phá và tìm cảm hứng cho bữa ăn."}</p>
    <span className="cb-category-cta">Khám phá công thức <span aria-hidden="true">↗</span></span>
  </Link>;
}
