import ContentState from "@/components/shared/ContentState";
import CategoryManager from "@/components/admin/CategoryManager";
import { getCategories } from "@/services/category-service";
import type { Category } from "@/types/category";

export default async function AdminCategoriesPage() {
  let categories: Category[] = [];

  try {
    categories = await getCategories();
  } catch {
    return <div className="cb-container cb-section"><ContentState title="Không thể tải danh mục" description="Dữ liệu tạm thời chưa tải được. Vui lòng thử lại trước khi thực hiện thay đổi." retry /></div>;
  }

  return (
    <CategoryManager
      initialCategories={categories}
    />
  );
}