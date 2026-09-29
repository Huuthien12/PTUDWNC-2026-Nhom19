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
      className="cb-form"
      aria-busy={isSubmitting}
    >
      <h2 className="cb-section-title">
        {category
          ? "Cập nhật danh mục"
          : "Thêm danh mục"}
      </h2>

      <div className="cb-field">
        <label
          htmlFor="category-name"
          className=""
        >
          Tên danh mục
        </label>

        <input
          id="category-name"
          value={name}
          onChange={(event) =>
            setName(event.target.value)
          }
          aria-describedby="category-name-hint"
          autoFocus
          minLength={2}
          maxLength={50}
          required
          disabled={isSubmitting}
          className="cb-input"
          placeholder="Ví dụ: Món Việt"
        />

        <p id="category-name-hint" className="cb-field-hint">
          Từ 2 đến 50 ký tự.
        </p>
      </div>

      <div className="cb-field">
        <label
          htmlFor="category-description"
          className=""
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
          className="cb-input resize-y"
          placeholder="Mô tả ngắn về danh mục..."
        />
      </div>

      {category && (
        <div className="mt-4 bg-cream p-4 text-sm text-muted break-words">
          Slug hiện tại:{" "}
          <strong>{category.slug}</strong>

          <p className="cb-field-hint">
            Slug không thay đổi khi sửa tên.
          </p>
        </div>
      )}

      <div className="mt-6 flex flex-wrap gap-3">
        <button
          type="submit"
          disabled={isSubmitting}
          className="cb-button"
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
          className="cb-button cb-button-secondary"
        >
          Hủy
        </button>
      </div>
    </form>
  );
}