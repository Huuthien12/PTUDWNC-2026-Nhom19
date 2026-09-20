import Link from "next/link";
import type { Category } from "@/types/category";

interface CategoryCardProps {
  category: Category;
}

export default function CategoryCard({
  category,
}: CategoryCardProps) {
  return (
    <Link
      href={`/categories/${category.slug}`}
      className="group block rounded-2xl border border-gray-200 bg-white p-6 shadow-sm transition hover:-translate-y-1 hover:shadow-lg"
    >
      <div className="mb-4 flex items-start justify-between gap-4">
        <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-orange-100 text-xl">
          🍽️
        </div>

        <span className="rounded-full bg-gray-100 px-3 py-1 text-sm text-gray-600">
          {category.recipeCount} công thức
        </span>
      </div>

      <h2 className="mb-2 text-xl font-bold text-gray-900 transition group-hover:text-orange-600">
        {category.name}
      </h2>

      <p className="min-h-12 text-sm leading-6 text-gray-600">
        {category.description?.trim() ||
          "Khám phá các công thức trong danh mục này."}
      </p>

      <div className="mt-5 font-medium text-orange-600">
        Xem công thức →
      </div>
    </Link>
  );
}