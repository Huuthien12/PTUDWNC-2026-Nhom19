import CategoryList from "@/components/categories/CategoryList";
import { getCategories } from "@/services/category-service";
import type { Category } from "@/types/category";

export default async function CategoriesPage() {
  let categories: Category[] = [];
  let hasError = false;

  try {
    categories = await getCategories();
  } catch {
    hasError = true;
  }

  if (hasError) {
    return (
      <main className="flex min-h-screen items-center justify-center bg-gray-50 px-6">
        <div className="max-w-md rounded-2xl border border-red-200 bg-white p-8 text-center shadow-sm">
          <h1 className="text-xl font-bold text-gray-900">
            Không thể tải danh mục
          </h1>

          <p className="mt-3 text-gray-600">
            Không thể kết nối đến hệ thống. Hãy kiểm tra Backend
            và thử lại.
          </p>
        </div>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-gray-50">
      <section className="border-b border-gray-200 bg-white">
        <div className="mx-auto max-w-7xl px-6 py-14">
          <p className="mb-3 font-semibold text-orange-600">
            Culinary Blog
          </p>

          <h1 className="text-4xl font-bold tracking-tight text-gray-900">
            Danh mục công thức
          </h1>

          <p className="mt-4 max-w-2xl leading-7 text-gray-600">
            Khám phá các món ăn theo từng danh mục và tìm
            công thức phù hợp với bạn.
          </p>
        </div>
      </section>

      <section className="mx-auto max-w-7xl px-6 py-10">
        <div className="mb-6 flex items-center justify-between">
          <h2 className="text-xl font-bold text-gray-900">
            Tất cả danh mục
          </h2>

          <span className="text-sm text-gray-500">
            {categories.length} danh mục
          </span>
        </div>

        <CategoryList categories={categories} />
      </section>
    </main>
  );
}