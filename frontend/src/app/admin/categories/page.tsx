"use client";

import { useEffect, useState } from "react";
import ContentState from "@/components/shared/ContentState";
import CategoryManager from "@/components/admin/CategoryManager";
import { getCategories } from "@/services/category-service";
import type { Category } from "@/types/category";

export default function AdminCategoriesPage() {
  const [categories, setCategories] = useState<Category[] | null>(null);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    let active = true;
    getCategories().then((data) => { if (active) setCategories(data); })
      .catch(() => { if (active) setFailed(true); });
    return () => { active = false; };
  }, []);

  if (failed) {
    return <div className="cb-container cb-section"><ContentState title="Không thể tải danh mục" description="Dữ liệu tạm thời chưa tải được. Vui lòng thử lại trước khi thực hiện thay đổi." retry /></div>;
  }
  if (!categories) return <p role="status" className="p-8">Đang tải danh mục...</p>;
  return <CategoryManager initialCategories={categories} />;
}
