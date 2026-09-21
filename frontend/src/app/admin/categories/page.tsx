import CategoryManager from "@/components/admin/CategoryManager";
import { getCategories } from "@/services/category-service";
import type { Category } from "@/types/category";

export default async function AdminCategoriesPage() {
  let categories: Category[] = [];

  try {
    categories = await getCategories();
  } catch {
    categories = [];
  }

  return (
    <CategoryManager
      initialCategories={categories}
    />
  );
}