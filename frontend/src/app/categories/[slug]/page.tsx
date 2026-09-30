import Link from "next/link";
import CulinaryIllustration from "@/components/shared/CulinaryIllustration";
import ContentState from "@/components/shared/ContentState";
import { notFound } from "next/navigation";

import CategoryPagination from "@/components/categories/CategoryPagination";
import CategoryRecipeCard from "@/components/categories/CategoryRecipeCard";
import { ApiError } from "@/services/api-client";
import { getCategoryBySlug } from "@/services/category-service";
import type { CategoryDetail } from "@/types/category";

interface CategoryDetailPageProps {
  params: Promise<{
    slug: string;
  }>;

  searchParams: Promise<{
    page?: string;
  }>;
}

export default async function CategoryDetailPage({
  params,
  searchParams,
}: CategoryDetailPageProps) {
  const { slug } = await params;
  const { page: pageValue } = await searchParams;

  const parsedPage = Number(pageValue ?? "1");

  const page =
    Number.isInteger(parsedPage) && parsedPage >= 1
      ? parsedPage
      : 1;

  let detail: CategoryDetail | null = null;
  let categoryNotFound = false;
  let hasError = false;

  try {
    detail = await getCategoryBySlug(
      slug,
      page,
      12
    );
  } catch (error) {
    if (
      error instanceof ApiError &&
      error.status === 404
    ) {
      categoryNotFound = true;
    } else {
      hasError = true;
    }
  }

  if (categoryNotFound) {
    notFound();
  }

  if (hasError || detail === null) {
    return <div className="cb-container cb-section"><ContentState title="Chưa thể tải danh mục" description="Đã có lỗi khi tải dữ liệu. Vui lòng thử lại sau một chút." href="/categories" action="Quay lại danh mục" /></div>;
  }
  const { category, recipes } = detail;
  return <>
    <section className="cb-hero"><div className="cb-container cb-hero-inner">
      <div>
        <Link href="/categories" className="cb-text-link">← Tất cả danh mục</Link>
        <p className="cb-eyebrow mt-8">Danh mục</p>
        <h1 className="cb-title">{category.name}</h1>
        <p className="cb-lead">{category.description || "Khám phá những gợi ý cho bữa ăn của bạn."}</p>
        <p className="mt-6 text-sm text-forest">{category.recipeCount} công thức đã xuất bản</p>
      </div>
      <div className="cb-hero-art"><CulinaryIllustration /></div>
    </div></section>
    <section className="cb-container cb-section" aria-labelledby="recipes-heading">
      <div className="cb-section-heading"><h2 id="recipes-heading" className="cb-section-title">Cảm hứng vào bếp</h2><span className="text-sm text-muted">{recipes.totalCount} công thức</span></div>
      {recipes.items.length === 0 ? (
        <div className="cb-state">
          <h2 className="cb-section-title">
            {recipes.totalCount === 0 ? "Danh mục chưa có công thức" : "Chưa có công thức ở trang này"}
          </h2>
          <p>{recipes.totalCount === 0
            ? "Bạn có thể khám phá các danh mục khác để tìm thêm cảm hứng vào bếp."
            : "Quay về trang đầu để xem các công thức trong danh mục."}</p>
          <Link className="cb-text-link" href={recipes.totalCount === 0 ? "/categories" : `/categories/${category.slug}`}>
            {recipes.totalCount === 0 ? "Khám phá danh mục khác →" : "Về trang đầu →"}
          </Link>
        </div>
      ) :
        <div className="cb-grid">{recipes.items.map(recipe => <CategoryRecipeCard key={recipe.id} recipe={recipe} />)}</div>}
      <CategoryPagination slug={category.slug} currentPage={recipes.page} totalPages={recipes.totalPages} />
    </section>
  </>;
}
