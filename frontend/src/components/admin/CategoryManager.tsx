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

    try {
      await deleteCategory(category.id);

      setSuccessMessage(
        `Đã xóa danh mục "${category.name}".`
      );

      await refreshCategories();
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  }

  return (
    <main className="min-h-screen bg-gray-50">
      <header className="border-b border-gray-200 bg-white">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-6 py-8">
          <div>
            <p className="font-semibold text-orange-600">
              Culinary Blog Admin
            </p>

            <h1 className="mt-1 text-3xl font-bold text-gray-900">
              Quản lý danh mục
            </h1>
          </div>

          <Link
            href="/categories"
            className="text-sm font-semibold text-gray-600 hover:text-orange-600"
          >
            Xem trang công khai →
          </Link>
        </div>
      </header>

      <section className="mx-auto max-w-7xl px-6 py-10">
        <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
          <div>
            <h2 className="text-xl font-bold text-gray-900">
              Danh sách Category
            </h2>

            <p className="mt-1 text-sm text-gray-500">
              {categories.length} danh mục
            </p>
          </div>

          <button
            type="button"
            onClick={openCreateForm}
            className="rounded-xl bg-orange-600 px-5 py-3 font-semibold text-white transition hover:bg-orange-700"
          >
            + Thêm danh mục
          </button>
        </div>

        {errorMessage && (
          <div className="mb-6 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-red-700">
            {errorMessage}
          </div>
        )}

        {successMessage && (
          <div className="mb-6 rounded-xl border border-green-200 bg-green-50 px-4 py-3 text-green-700">
            {successMessage}
          </div>
        )}

        {showForm && (
          <div className="mb-8">
            <CategoryForm
              key={editingCategory?.id ?? "new"}
              category={editingCategory}
              isSubmitting={isSubmitting}
              onSubmit={handleSubmit}
              onCancel={closeForm}
            />
          </div>
        )}

        <div className="overflow-x-auto rounded-2xl border border-gray-200 bg-white shadow-sm">
          <table className="w-full text-left">
            <thead className="border-b border-gray-200 bg-gray-50">
              <tr>
                <th className="px-5 py-4 text-sm font-semibold text-gray-700">
                  Tên
                </th>

                <th className="px-5 py-4 text-sm font-semibold text-gray-700">
                  Slug
                </th>

                <th className="px-5 py-4 text-sm font-semibold text-gray-700">
                  Công thức
                </th>

                <th className="px-5 py-4 text-right text-sm font-semibold text-gray-700">
                  Thao tác
                </th>
              </tr>
            </thead>

            <tbody className="divide-y divide-gray-100">
              {categories.map((category) => (
                <tr
                  key={category.id}
                  className="hover:bg-gray-50"
                >
                  <td className="px-5 py-4">
                    <p className="font-semibold text-gray-900">
                      {category.name}
                    </p>

                    <p className="mt-1 max-w-md text-sm text-gray-500">
                      {category.description ||
                        "Không có mô tả"}
                    </p>
                  </td>

                  <td className="px-5 py-4 text-sm text-gray-600">
                    {category.slug}
                  </td>

                  <td className="px-5 py-4 text-sm text-gray-600">
                    {category.recipeCount}
                  </td>

                  <td className="px-5 py-4">
                    <div className="flex justify-end gap-2">
                      <button
                        type="button"
                        onClick={() =>
                          openEditForm(category)
                        }
                        className="rounded-lg border border-gray-300 px-3 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-50"
                      >
                        Sửa
                      </button>

                      <button
                        type="button"
                        onClick={() =>
                          void handleDelete(category)
                        }
                        className="rounded-lg border border-red-200 px-3 py-2 text-sm font-semibold text-red-600 hover:bg-red-50"
                      >
                        Xóa
                      </button>
                    </div>
                  </td>
                </tr>
              ))}

              {categories.length === 0 && (
                <tr>
                  <td
                    colSpan={4}
                    className="px-5 py-12 text-center text-gray-500"
                  >
                    Chưa có danh mục.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>
    </main>
  );
}