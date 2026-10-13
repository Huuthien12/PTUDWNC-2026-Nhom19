
"use client";

import { useState } from "react";
import type { FormEvent } from "react";
import type { RecipeStep } from "@/types/recipe";
import {
  createRecipeStep,
  deleteRecipeStep,
  updateRecipeStep,
} from "@/services/recipe-step-service";

interface RecipeStepManagerProps {
  recipeId: string;
  initialSteps: RecipeStep[];
  recipeRowVersion: string;
}

interface StepForm {
  title: string;
  description: string;
  timerMinutes: string;
}

const emptyForm: StepForm = {
  title: "",
  description: "",
  timerMinutes: "",
};

function sortSteps(steps: RecipeStep[]): RecipeStep[] {
  return [...steps].sort((a, b) => a.stepNumber - b.stepNumber);
}

function formFromStep(step: RecipeStep): StepForm {
  return {
    title: step.title ?? "",
    description: step.description,
    timerMinutes:
      step.timerMinutes == null ? "" : String(step.timerMinutes),
  };
}

export default function RecipeStepManager({
  recipeId,
  initialSteps,
  recipeRowVersion,
}: RecipeStepManagerProps) {
  const [steps, setSteps] = useState<RecipeStep[]>(() =>
    sortSteps(initialSteps),
  );

  const [currentRecipeRowVersion, setCurrentRecipeRowVersion] =
    useState(recipeRowVersion);

  const [form, setForm] = useState<StepForm>(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");

  function resetForm() {
    setForm(emptyForm);
    setEditingId(null);
  }

  function updateField(field: keyof StepForm, value: string) {
    setForm((current) => ({
      ...current,
      [field]: value,
    }));
  }

  function validateForm(): string | null {
    if (!form.description.trim()) {
      return "Mô tả bước nấu không được để trống.";
    }

    if (form.timerMinutes.trim()) {
      const timer = Number(form.timerMinutes);

      if (!Number.isInteger(timer) || timer < 0) {
        return "Thời gian phải là số nguyên lớn hơn hoặc bằng 0.";
      }
    }

    return null;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const validationError = validateForm();

    if (validationError) {
      setError(validationError);
      setMessage("");
      return;
    }

    if (!currentRecipeRowVersion) {
      setError("Thiếu RowVersion của công thức. Hãy tải lại trang.");
      return;
    }

    setPending(true);
    setError("");
    setMessage("");

    try {
      const timerMinutes = form.timerMinutes.trim()
        ? Number(form.timerMinutes)
        : null;

      const result = editingId
        ? await updateRecipeStep(recipeId, editingId, {
            stepNumber:
              steps.find((step) => step.id === editingId)?.stepNumber ?? 1,
            title: form.title.trim() || null,
            description: form.description.trim(),
            timerMinutes,
            rowVersion: currentRecipeRowVersion,
          })
        : await createRecipeStep(recipeId, {
            title: form.title.trim() || null,
            description: form.description.trim(),
            timerMinutes,
            rowVersion: currentRecipeRowVersion,
          });

      setSteps(sortSteps(result.items));
      setCurrentRecipeRowVersion(result.rowVersion);
      setMessage(
        editingId
          ? "Đã cập nhật bước nấu."
          : "Đã thêm bước nấu.",
      );
      resetForm();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Không thể lưu bước nấu.",
      );
    } finally {
      setPending(false);
    }
  }

  function startEdit(step: RecipeStep) {
    setEditingId(step.id);
    setForm(formFromStep(step));
    setError("");
    setMessage("");

    window.scrollTo({
      top: document.body.scrollHeight,
      behavior: "smooth",
    });
  }

  async function handleDelete(step: RecipeStep) {
    const confirmed = window.confirm(
      `Bạn có chắc muốn xóa bước ${step.stepNumber} không?`,
    );

    if (!confirmed) return;

    if (!currentRecipeRowVersion) {
      setError("Thiếu RowVersion của công thức. Hãy tải lại trang.");
      return;
    }

    setPending(true);
    setError("");
    setMessage("");

    try {
      await deleteRecipeStep(
        recipeId,
        step.id,
        currentRecipeRowVersion,
      );

      // Endpoint DELETE trả 204 và RowVersion mới qua header ETag.
      // Tải lại trang để lấy danh sách và RowVersion mới nhất.
      window.location.reload();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Không thể xóa bước nấu.",
      );
      setPending(false);
    }
  }

  return (
    <section className="space-y-6 rounded-xl border bg-white p-6">
      <div>
        <h2 className="text-xl font-bold text-gray-900">
          Các bước nấu
        </h2>
        <p className="mt-1 text-sm text-gray-600">
          Thêm, chỉnh sửa và xóa các bước thực hiện món ăn.
        </p>
      </div>

      {error && (
        <p
          role="alert"
          className="rounded-lg bg-red-50 p-3 text-sm text-red-700"
        >
          {error}
        </p>
      )}

      {message && (
        <p
          role="status"
          className="rounded-lg bg-green-50 p-3 text-sm text-green-700"
        >
          {message}
        </p>
      )}

      <div className="space-y-4">
        {steps.length === 0 ? (
          <p className="rounded-lg border border-dashed p-6 text-center text-gray-500">
            Chưa có bước nấu nào.
          </p>
        ) : (
          steps.map((step) => (
            <article
              key={step.id}
              className="rounded-xl border p-4"
            >
              <div className="flex flex-col gap-4 sm:flex-row sm:items-start">
                <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-orange-100 font-bold text-orange-700">
                  {step.stepNumber}
                </div>

                <div className="min-w-0 flex-1">
                  <h3 className="font-semibold text-gray-900">
                    {step.title || `Bước ${step.stepNumber}`}
                  </h3>

                  <p className="mt-2 whitespace-pre-wrap text-gray-700">
                    {step.description}
                  </p>

                  {step.timerMinutes != null && (
                    <p className="mt-2 text-sm text-gray-500">
                      Thời gian: {step.timerMinutes} phút
                    </p>
                  )}
                </div>

                <div className="flex flex-wrap gap-2">
                  <button
                    type="button"
                    onClick={() => startEdit(step)}
                    disabled={pending}
                    className="rounded-lg border border-blue-300 px-3 py-2 text-sm text-blue-700 disabled:opacity-40"
                  >
                    Sửa
                  </button>

                  <button
                    type="button"
                    onClick={() => void handleDelete(step)}
                    disabled={pending}
                    className="rounded-lg border border-red-300 px-3 py-2 text-sm text-red-700 disabled:opacity-40"
                  >
                    Xóa
                  </button>
                </div>
              </div>
            </article>
          ))
        )}
      </div>

      <form
        onSubmit={(event) => void handleSubmit(event)}
        className="space-y-4 rounded-xl border-t pt-6"
      >
        <h3 className="font-semibold text-gray-900">
          {editingId ? "Chỉnh sửa bước nấu" : "Thêm bước nấu"}
        </h3>

        <div>
          <label
            htmlFor="recipe-step-title"
            className="mb-2 block font-medium"
          >
            Tiêu đề bước
          </label>

          <input
            id="recipe-step-title"
            value={form.title}
            onChange={(event) =>
              updateField("title", event.target.value)
            }
            disabled={pending}
            className="w-full rounded-lg border border-gray-300 bg-white p-3 text-gray-900 focus:outline-orange-500"
            placeholder="Ví dụ: Sơ chế nguyên liệu"
          />
        </div>

        <div>
          <label
            htmlFor="recipe-step-description"
            className="mb-2 block font-medium"
          >
            Mô tả bước *
          </label>

          <textarea
            id="recipe-step-description"
            value={form.description}
            onChange={(event) =>
              updateField("description", event.target.value)
            }
            disabled={pending}
            rows={4}
            required
            className="w-full rounded-lg border border-gray-300 bg-white p-3 text-gray-900 focus:outline-orange-500"
            placeholder="Mô tả chi tiết cách thực hiện..."
          />
        </div>

        <div>
          <label
            htmlFor="recipe-step-timer"
            className="mb-2 block font-medium"
          >
            Thời gian (phút)
          </label>

          <input
            id="recipe-step-timer"
            type="number"
            min="0"
            step="1"
            value={form.timerMinutes}
            onChange={(event) =>
              updateField("timerMinutes", event.target.value)
            }
            disabled={pending}
            className="w-full rounded-lg border border-gray-300 bg-white p-3 text-gray-900 focus:outline-orange-500"
          />
        </div>

        <div className="flex gap-3">
          <button
            type="submit"
            disabled={pending}
            className="rounded-lg bg-orange-600 px-5 py-3 font-semibold text-white disabled:opacity-50"
          >
            {pending
              ? "Đang xử lý..."
              : editingId
                ? "Lưu bước"
                : "Thêm bước"}
          </button>

          {editingId && (
            <button
              type="button"
              onClick={resetForm}
              disabled={pending}
              className="rounded-lg border px-5 py-3 font-semibold disabled:opacity-50"
            >
              Hủy
            </button>
          )}
        </div>
      </form>
    </section>
  );
}
