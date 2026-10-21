"use client";

import { useState, type FormEvent } from "react";
import type { RecipeDetail, RecipeStep } from "@/types/recipe";
import { createRecipeStep, deleteRecipeStep, updateRecipeStep,
  type RecipeStepMutationResult } from "@/services/recipe-step-service";
import type { RecipeMutationLock } from "./useRecipeMutation";

interface RecipeStepManagerProps {
  recipeId: string;
  initialSteps: RecipeStep[];
  rowVersion: string;
  mutation: RecipeMutationLock;
  onRowVersionChange: (rowVersion: string) => void;
  onReload: () => Promise<RecipeDetail>;
}

interface StepForm { title: string; description: string; timerMinutes: string }
const emptyForm: StepForm = { title: "", description: "", timerMinutes: "" };
const sortSteps = (steps: RecipeStep[]) => [...steps].sort((a, b) => a.stepNumber - b.stepNumber);

export default function RecipeStepManager(props: RecipeStepManagerProps) {
  return <RecipeStepManagerState key={props.recipeId} {...props} />;
}

function RecipeStepManagerState({ recipeId, initialSteps, rowVersion, mutation,
  onRowVersionChange, onReload }: RecipeStepManagerProps) {
  const [steps, setSteps] = useState(() => sortSteps(initialSteps));
  const [previousSteps, setPreviousSteps] = useState(initialSteps);
  const [form, setForm] = useState<StepForm>(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const blocked = mutation.pending || mutation.needsReload;

  // Reconcile new server data before committing children; preserve the form draft.
  if (initialSteps !== previousSteps) {
    setPreviousSteps(initialSteps);
    setSteps(sortSteps(initialSteps));
  }

  function resetForm() { setForm(emptyForm); setEditingId(null); }
  function acknowledge(result: RecipeStepMutationResult) {
    setSteps(sortSteps(result.items));
    onRowVersionChange(result.rowVersion);
    mutation.invalidate();
  }
  async function reload() {
    const recipe = await onReload();
    setSteps(sortSteps(recipe.steps));
  }
  async function run(success: string, action: () => Promise<void>) {
    if (!mutation.begin()) return;
    setError(""); setMessage("");
    try {
      await action();
      mutation.invalidate();
      await reload();
      setMessage(success);
    } catch (reason) {
      mutation.invalidate();
      let recovered = false;
      try { await reload(); recovered = true; } catch { /* Keep aggregate mutations blocked until reload succeeds. */ }
      setError(`${reason instanceof Error ? reason.message : "Không thể cập nhật bước nấu."}${recovered ? "" : " Hãy tải lại dữ liệu trước khi thao tác tiếp."}`);
    } finally { mutation.end(); }
  }

  function validateForm() {
    if (!form.description.trim()) return "Mô tả bước nấu không được để trống.";
    if (form.description.trim().length > 2000) return "Mô tả bước nấu không được vượt quá 2000 ký tự.";
    if (form.title.trim().length > 200) return "Tiêu đề bước không được vượt quá 200 ký tự.";
    if (form.timerMinutes.trim()) {
      const timer = Number(form.timerMinutes);
      if (!Number.isInteger(timer) || timer < 0) return "Thời gian phải là số nguyên lớn hơn hoặc bằng 0.";
    }
    return null;
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const validationError = validateForm();
    if (validationError) { setError(validationError); setMessage(""); return; }
    if (!rowVersion) { setError("Thiếu RowVersion của công thức. Hãy tải lại trang."); return; }
    const input = { title: form.title.trim() || null, description: form.description.trim(),
      timerMinutes: form.timerMinutes.trim() ? Number(form.timerMinutes) : null, rowVersion };
    void run(editingId ? "Đã cập nhật bước nấu." : "Đã thêm bước nấu.", async () => {
      const result = editingId
        ? await updateRecipeStep(recipeId, editingId, { ...input,
          stepNumber: steps.find((step) => step.id === editingId)?.stepNumber ?? 1 })
        : await createRecipeStep(recipeId, input);
      acknowledge(result);
      resetForm();
    });
  }

  function startEdit(step: RecipeStep) {
    if (mutation.isBlocked()) return;
    setEditingId(step.id);
    setForm({ title: step.title ?? "", description: step.description,
      timerMinutes: step.timerMinutes == null ? "" : String(step.timerMinutes) });
    setError(""); setMessage("");
  }

  function remove(step: RecipeStep) {
    if (mutation.isBlocked() || !window.confirm(`Bạn có chắc muốn xóa bước ${step.stepNumber} không?`)) return;
    void run("Đã xóa bước nấu và cập nhật lại thứ tự.", async () => {
      const nextVersion = await deleteRecipeStep(recipeId, step.id, rowVersion);
      mutation.invalidate();
      if (nextVersion) onRowVersionChange(nextVersion);
      setSteps((current) => current.filter((item) => item.id !== step.id)
        .map((item, index) => ({ ...item, stepNumber: index + 1 })));
    });
  }

  async function retryReload() {
    if (!mutation.begin(true)) return;
    try { await reload(); setMessage("Đã tải lại các bước và RowVersion mới nhất."); setError(""); }
    catch { setError("Chưa tải lại được dữ liệu. Kiểm tra kết nối và thử lại."); }
    finally { mutation.end(); }
  }

  return <section className="space-y-6 rounded-xl border bg-white p-6" aria-busy={mutation.pending}>
    <div><h2 className="text-xl font-bold text-gray-900">Các bước nấu</h2>
      <p className="mt-1 text-sm text-gray-600">Bước mới được thêm cuối danh sách; khi xóa, thứ tự được cập nhật liên tục.</p></div>
    {error && <p role="alert" className="rounded-lg bg-red-50 p-3 text-sm text-red-700">{error}</p>}
    {message && <p role="status" className="rounded-lg bg-green-50 p-3 text-sm text-green-700">{message}</p>}
    {mutation.needsReload && <button type="button" disabled={mutation.pending} onClick={() => void retryReload()}
      className="rounded-lg border px-4 py-2">Tải lại dữ liệu</button>}
    <div className="space-y-4">{steps.length === 0
      ? <p className="rounded-lg border border-dashed p-6 text-center text-gray-500">Chưa có bước nấu nào.</p>
      : steps.map((step) => <article key={step.id} className="rounded-xl border p-4">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-start">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-orange-100 font-bold text-orange-700">{step.stepNumber}</div>
          <div className="min-w-0 flex-1"><h3 className="font-semibold text-gray-900">{step.title || `Bước ${step.stepNumber}`}</h3>
            <p className="mt-2 whitespace-pre-wrap text-gray-700">{step.description}</p>
            {step.timerMinutes != null && <p className="mt-2 text-sm text-gray-500">Thời gian: {step.timerMinutes} phút</p>}</div>
          <div className="flex flex-wrap gap-2">
            <button type="button" onClick={() => startEdit(step)} disabled={blocked}
              className="rounded-lg border border-blue-300 px-3 py-2 text-sm text-blue-700 disabled:opacity-40">Sửa</button>
            <button type="button" onClick={() => remove(step)} disabled={blocked}
              className="rounded-lg border border-red-300 px-3 py-2 text-sm text-red-700 disabled:opacity-40">Xóa</button>
          </div>
        </div>
      </article>)}</div>
    <form onSubmit={submit} className="space-y-4 rounded-xl border-t pt-6">
      <h3 className="font-semibold text-gray-900">{editingId ? "Chỉnh sửa bước nấu" : "Thêm bước nấu"}</h3>
      <label className="block space-y-2 font-medium" htmlFor="recipe-step-title">Tiêu đề bước
        <input id="recipe-step-title" value={form.title} maxLength={200} disabled={blocked}
          onChange={(event) => setForm((current) => ({ ...current, title: event.target.value }))}
          className="w-full rounded-lg border border-gray-300 bg-white p-3 text-gray-900 focus:outline-orange-500" /></label>
      <label className="block space-y-2 font-medium" htmlFor="recipe-step-description">Mô tả bước *
        <textarea id="recipe-step-description" value={form.description} maxLength={2000} rows={4} required disabled={blocked}
          onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))}
          className="w-full rounded-lg border border-gray-300 bg-white p-3 text-gray-900 focus:outline-orange-500" /></label>
      <label className="block space-y-2 font-medium" htmlFor="recipe-step-timer">Thời gian (phút)
        <input id="recipe-step-timer" type="number" min="0" step="1" value={form.timerMinutes} disabled={blocked}
          onChange={(event) => setForm((current) => ({ ...current, timerMinutes: event.target.value }))}
          className="w-full rounded-lg border border-gray-300 bg-white p-3 text-gray-900 focus:outline-orange-500" /></label>
      <div className="flex gap-3"><button type="submit" disabled={blocked}
        className="rounded-lg bg-orange-600 px-5 py-3 font-semibold text-white disabled:opacity-50">
        {mutation.pending ? "Đang xử lý..." : editingId ? "Lưu bước" : "Thêm bước"}</button>
        {editingId && <button type="button" onClick={resetForm} disabled={blocked}
          className="rounded-lg border px-5 py-3 font-semibold disabled:opacity-50">Hủy</button>}</div>
    </form>
  </section>;
}
