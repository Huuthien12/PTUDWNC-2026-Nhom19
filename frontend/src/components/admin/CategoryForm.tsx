"use client";

import { FormEvent, useState } from "react";

import type { Category } from "@/types/category";

interface CategoryFormProps {
  category?: Category | null;
  isSubmitting: boolean;
  onSubmit: (
    name: string,
    description: string
  ) => Promise<void>;
  onCancel: () => void;
}

export default function CategoryForm({
  category,
  isSubmitting,
  onSubmit,
  onCancel,
}: CategoryFormProps) {
  const [name, setName] = useState(
    category?.name ?? ""
  );

  const [description, setDescription] = useState(
    category?.description ?? ""
  );

  async function handleSubmit(
    event: FormEvent<HTMLFormElement>
  ) {
    event.preventDefault();

    await onSubmit(
      name.trim(),
      description.trim()
    );
  }

  return (
    <form
      onSubmit={handleSubmit}
      className="rounded-2xl border border-gray-200 bg-white p-6 shadow-sm"
    >
      <h2 className="text-xl font-bold text-gray-900">
        {category
          ? "Cập nhật danh mục"
          : "Thêm danh mục"}
      </h2>

      <div className="mt-5">
        <label
          htmlFor="category-name"
          className="mb-2 block text-sm font-semibold text-gray-700"
        >
          Tên danh mục
        </label>

        <input
          id="category-name"
          value={name}
          onChange={(event) =>
            setName(event.target.value)
          }
          minLength={2}
          maxLength={50}
          required
          disabled={isSubmitting}
          className="w-full rounded-xl border border-gray-300 px-4 py-3 text-gray-900 outline-none transition focus:border-orange-500"
          placeholder="Ví dụ: Món Việt"
        />

        <p className="mt-1 text-xs text-gray-500">
          Từ 2 đến 50 ký tự.
        </p>
      </div>

      <div className="mt-5">
        <label
          htmlFor="category-description"
          className="mb-2 block text-sm font-semibold text-gray-700"
        >
          Mô tả
        </label>

        <textarea
          id="category-description"
          value={description}
          onChange={(event) =>
            setDescription(event.target.value)
          }
          disabled={isSubmitting}
          rows={4}
          className="w-full resize-none rounded-xl border border-gray-300 px-4 py-3 text-gray-900 outline-none transition focus:border-orange-500"
          placeholder="Mô tả ngắn về danh mục..."
        />
      </div>

      {category && (
        <div className="mt-4 rounded-xl bg-gray-50 p-3 text-sm text-gray-600">
          Slug hiện tại:{" "}
          <strong>{category.slug}</strong>

          <p className="mt-1 text-xs text-gray-500">
            Slug không thay đổi khi sửa tên.
          </p>
        </div>
      )}

      <div className="mt-6 flex gap-3">
        <button
          type="submit"
          disabled={isSubmitting}
          className="rounded-xl bg-orange-600 px-5 py-3 font-semibold text-white transition hover:bg-orange-700 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {isSubmitting
            ? "Đang xử lý..."
            : category
              ? "Lưu thay đổi"
              : "Thêm danh mục"}
        </button>

        <button
          type="button"
          onClick={onCancel}
          disabled={isSubmitting}
          className="rounded-xl border border-gray-300 px-5 py-3 font-semibold text-gray-700 hover:bg-gray-50"
        >
          Hủy
        </button>
      </div>
    </form>
  );
}