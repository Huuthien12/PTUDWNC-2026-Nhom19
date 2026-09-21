import CategoryCard from "@/components/categories/CategoryCard";
import type { Category } from "@/types/category";

interface CategoryListProps {
  categories: Category[];
}

export default function CategoryList({
  categories,
}: CategoryListProps) {
  if (categories.length === 0) {
    return (
      <div className="rounded-2xl border border-dashed border-gray-300 bg-white p-12 text-center">
        <p className="text-lg font-semibold text-gray-800">
          Chưa có danh mục
        </p>

        <p className="mt-2 text-gray-500">
          Danh mục công thức sẽ xuất hiện tại đây.
        </p>
      </div>
    );
  }

  return (
    <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
      {categories.map((category) => (
        <CategoryCard
          key={category.id}
          category={category}
        />
      ))}
    </div>
  );
}