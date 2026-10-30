"use client";

import { useState } from "react";
import Link from "next/link";

import CategoryForm from "@/components/admin/CategoryForm";
import {
  createCategory,
  deleteCategory,
  getCategories,
  updateCategory,
} from "@/services/category-service";
import type { Category } from "@/types/category";

interface CategoryManagerProps {
  initialCategories: Category[];
}

function getErrorMessage(error: unknown): string {
  if (
    typeof error !== "object" ||
    error === null ||
    !("status" in error) ||
    typeof error.status !== "number"
  ) {
    return "Đã xảy ra lỗi. Vui lòng thử lại.";
  }

  switch (error.status) {
    case 400:
      return "Dữ liệu không hợp lệ. Hãy kiểm tra lại thông tin.";

    case 401:
      return "Bạn chưa đăng nhập.";

    case 403:
      return "Bạn không có quyền Admin.";

    case 404:
      return "Danh mục không tồn tại.";

    case 409:
      return "Tên danh mục đã tồn tại hoặc danh mục vẫn còn công thức.";

    case 422:
      return "Dữ liệu đã được thay đổi. Hãy tải lại và thử lại.";

    default:
      return `Không thể thực hiện yêu cầu (${error.status}).`;
  }
}
export default function CategoryManager({
  initialCategories,
}: CategoryManagerProps) {
  const [categories, setCategories] =
    useState<Category[]>(initialCategories);

  const [deletingId, setDeletingId] = useState<string | null>(null);

  const [editingCategory, setEditingCategory] =
    useState<Category | null>(null);

  const [showForm, setShowForm] =
    useState(false);

  const [isSubmitting, setIsSubmitting] =
    useState(false);

  const [errorMessage, setErrorMessage] =
    useState("");

  const [successMessage, setSuccessMessage] =
    useState("");

  async function refreshCategories() {
    const data = await getCategories();
    setCategories(data);
  }

  function openCreateForm() {
    setEditingCategory(null);
    setErrorMessage("");
    setSuccessMessage("");
    setShowForm(true);
  }

  function openEditForm(category: Category) {
    setEditingCategory(category);
    setErrorMessage("");
    setSuccessMessage("");
    setShowForm(true);
  }

  function closeForm() {
    setEditingCategory(null);
    setShowForm(false);
  }

  async function handleSubmit(
    name: string,
    description: string
  ) {
    setIsSubmitting(true);
    setErrorMessage("");
    setSuccessMessage("");

    try {
      const input = {
        name,
        description:
          description.length > 0
            ? description
            : null,
      };

      if (editingCategory) {
        await updateCategory(
          editingCategory.id,
          input
        );

        setSuccessMessage(
          "Cập nhật danh mục thành công."
        );
      } else {
        await createCategory(input);

        setSuccessMessage(
          "Thêm danh mục thành công."
        );
      }

      closeForm();
      await refreshCategories();
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleDelete(category: Category) {
    const confirmed = window.confirm(
      `Bạn có chắc muốn xóa danh mục "${category.name}"?`
    );

    if (!confirmed) {
      return;
    }

    setErrorMessage("");
    setSuccessMessage("");

    setDeletingId(category.id);
    try {
      await deleteCategory(category.id);

      setSuccessMessage(
        `Đã xóa danh mục "${category.name}".`
      );

      await refreshCategories();
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    } finally {
      setDeletingId(null);
    }
  }

  const busy = isSubmitting || deletingId !== null;
  return <div className="cb-container cb-section">
    <header className="cb-section-heading">
      <div><p className="cb-eyebrow mb-3">Không gian quản trị</p><h1 className="cb-section-title">Quản lý danh mục</h1><p className="text-muted text-sm mt-2">Sắp xếp và chăm chút từng góc hương vị.</p></div>
      <Link href="/categories" className="cb-text-link">Xem trang công khai ↗</Link>
    </header>
    <div className="mb-8 flex flex-wrap items-center justify-between gap-4">
      <h2 className="text-sm text-muted">{categories.length} danh mục</h2>
      <button type="button" className="cb-button" onClick={openCreateForm} disabled={busy}>+ Thêm danh mục</button>
    </div>
    {errorMessage && <div className="cb-notice cb-notice-error" role="alert">{errorMessage}</div>}
    {successMessage && <div className="cb-notice" role="status">{successMessage}</div>}
    {showForm && <div className="mb-8"><CategoryForm key={editingCategory?.id ?? "new"} category={editingCategory} isSubmitting={busy} onSubmit={handleSubmit} onCancel={closeForm} /></div>}
    <div className="cb-admin-table" aria-busy={busy}>
      <table>
        <caption className="sr-only">Danh mục và các thao tác quản lý</caption>
        <thead><tr><th scope="col">Danh mục</th><th scope="col" className="cb-admin-slug">Đường dẫn</th><th scope="col">Công thức</th><th scope="col">Thao tác</th></tr></thead>
        <tbody>{categories.map(category => <tr key={category.id}>
          <td><p className="font-semibold">{category.name}</p><p className="mt-1 max-w-md text-sm text-muted">{category.description || "Chưa có mô tả."}</p></td>
          <td className="cb-admin-slug text-muted break-all">{category.slug}</td>
          <td>{category.recipeCount}</td>
          <td><div className="flex flex-wrap gap-2">
            <button type="button" className="cb-button cb-button-secondary" disabled={busy} onClick={() => openEditForm(category)} aria-label={`Sửa danh mục ${category.name}`}>Sửa</button>
            <button type="button" className="cb-button cb-button-danger" disabled={busy} onClick={() => void handleDelete(category)} aria-label={`Xóa danh mục ${category.name}`}>{deletingId === category.id ? "Đang xóa…" : "Xóa"}</button>
          </div></td>
        </tr>)}
        {!categories.length && <tr><td colSpan={4}><div className="py-8 text-center"><p className="cb-section-title">Chưa có danh mục</p><p className="mt-2 text-muted">Chọn “Thêm danh mục” để bắt đầu.</p></div></td></tr>}
        </tbody>
      </table>
    </div>
  </div>;
}
