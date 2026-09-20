import Link from "next/link";
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
    return (
      <main className="flex min-h-screen items-center justify-center bg-gray-50 px-6">
        <div className="max-w-md rounded-2xl border border-red-200 bg-white p-8 text-center shadow-sm">
          <h1 className="text-xl font-bold text-gray-900">
            Không thể tải danh mục
          </h1>

          <p className="mt-3 text-gray-600">
            Đã xảy ra lỗi khi tải dữ liệu. Vui lòng thử lại.
          </p>

          <Link
            href="/categories"
            className="mt-6 inline-block font-semibold text-orange-600 hover:text-orange-700"
          >
            ← Quay lại danh mục
          </Link>
        </div>
      </main>
    );
  }

  const { category, recipes } = detail;

  return (
    <main className="min-h-screen bg-gray-50">
      <section className="border-b border-gray-200 bg-white">
        <div className="mx-auto max-w-7xl px-6 py-12">
          <Link
            href="/categories"
            className="text-sm font-semibold text-orange-600 hover:text-orange-700"
          >
            ← Tất cả danh mục
          </Link>

          <div className="mt-6">
            <p className="font-semibold text-orange-600">
              Danh mục
            </p>

            <h1 className="mt-2 text-4xl font-bold tracking-tight text-gray-900">
              {category.name}
            </h1>

            <p className="mt-4 max-w-2xl leading-7 text-gray-600">
              {category.description ||
                "Khám phá các công thức trong danh mục này."}
            </p>

            <p className="mt-4 text-sm font-medium text-gray-500">
              {category.recipeCount} công thức đã xuất bản
            </p>
          </div>
        </div>
      </section>

      <section className="mx-auto max-w-7xl px-6 py-10">
        <div className="mb-6 flex items-center justify-between gap-4">
          <h2 className="text-2xl font-bold text-gray-900">
            Công thức
          </h2>

          <span className="text-sm text-gray-500">
            {recipes.totalCount} kết quả
          </span>
        </div>

        {recipes.items.length === 0 ? (
          <div className="rounded-2xl border border-dashed border-gray-300 bg-white px-6 py-16 text-center">
            <div className="text-5xl">
              🍲
            </div>

            <h2 className="mt-4 text-xl font-bold text-gray-900">
              Chưa có công thức
            </h2>

            <p className="mt-2 text-gray-500">
              Hiện chưa có công thức nào trong danh mục này.
            </p>
          </div>
        ) : (
          <>
            <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
              {recipes.items.map((recipe) => (
                <CategoryRecipeCard
                  key={recipe.id}
                  recipe={recipe}
                />
              ))}
            </div>

            <CategoryPagination
              slug={category.slug}
              currentPage={recipes.page}
              totalPages={recipes.totalPages}
            />
          </>
        )}
      </section>
    </main>
  );
}