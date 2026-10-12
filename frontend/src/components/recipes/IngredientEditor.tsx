"use client";

import { useState } from "react";
import { addRecipeIngredient, deleteRecipeIngredient, updateRecipeIngredient,
  type RecipeIngredientMutation } from "@/services/ingredient-service";
import { ingredientError, reconcileIngredients, validateIngredient,
  type EditableIngredient, type IngredientErrors, type IngredientField } from "@/services/ingredient-form";
import type { RecipeDetail, RecipeIngredient } from "@/types/recipe";
import type { RecipeMutationLock } from "./useRecipeMutation";

interface IngredientEditorProps {
  recipeId: string;
  initialIngredients: RecipeIngredient[];
  rowVersion: string;
  mutation: RecipeMutationLock;
  onRowVersionChange: (rowVersion: string) => void;
  onReload: () => Promise<RecipeDetail>;
}

const inputClass = "w-full rounded-lg border border-gray-300 bg-white p-3 text-gray-900 focus:outline-orange-500";

export default function IngredientEditor({ recipeId, initialIngredients, rowVersion, mutation,
  onRowVersionChange, onReload }: IngredientEditorProps) {
  const [model, setModel] = useState(() => ({ source: initialIngredients, server: initialIngredients,
    items: reconcileIngredients([], [], initialIngredients) }));
  const [draggedId, setDraggedId] = useState<string | null>(null);
  const [pendingId, setPendingId] = useState<string | null>(null);
  const [message, setMessage] = useState("");
  const [errors, setErrors] = useState<Record<string, IngredientErrors>>({});
  if (model.source !== initialIngredients) {
    setModel({ source: initialIngredients, server: initialIngredients,
      items: reconcileIngredients(model.items, model.server, initialIngredients) });
  }
  const items = model.items;
  const blocked = mutation.pending || mutation.needsReload;

  function setItems(change: (current: EditableIngredient[]) => EditableIngredient[]) {
    setModel((current) => ({ ...current, items: change(current.items) }));
  }

  function changeItem(clientId: string, field: IngredientField, value: string) {
    if (mutation.isBlocked()) return;
    setItems((current) => current.map((item) => item.clientId === clientId
      ? { ...item, [field]: field === "quantity" ? (value === "" ? null : Number(value)) : value } : item));
    setErrors((current) => ({ ...current, [clientId]: { ...current[clientId], [field]: undefined } }));
  }

  function moveBy(clientId: string, targetIndex: number) {
    if (mutation.isBlocked()) return;
    setItems((current) => {
      const index = current.findIndex((item) => item.clientId === clientId);
      if (index < 0 || targetIndex < 0 || targetIndex >= current.length) return current;
      const next = [...current];
      const [moved] = next.splice(index, 1);
      next.splice(targetIndex, 0, moved);
      return next.map((item, orderIndex) => ({ ...item, orderIndex }));
    });
    setDraggedId(null);
  }

  function acknowledge(saved: RecipeIngredientMutation, clientId: string, orderOnly = false) {
    const { sortOrder, rowVersion: nextVersion, ...content } = saved;
    const ingredient = { ...content, orderIndex: sortOrder };
    mutation.invalidate();
    onRowVersionChange(nextVersion);
    setModel((current) => ({ ...current,
      server: [...current.server.filter((item) => item.id !== saved.id), ingredient],
      items: current.items.map((item) => item.clientId === clientId
        ? orderOnly ? { ...item, orderIndex: sortOrder } : { ...ingredient, orderIndex: item.orderIndex, clientId } : item),
    }));
  }

  async function reload(resetOrder = false) {
    const recipe = await onReload();
    setModel((current) => ({ source: recipe.ingredients, server: recipe.ingredients,
      items: reconcileIngredients(current.items, current.server, recipe.ingredients, resetOrder) }));
  }

  async function run(id: string, success: string, action: () => Promise<void>) {
    if (!mutation.begin()) return;
    setPendingId(id);
    setMessage("");
    let completed = false;
    try {
      await action();
      completed = true;
      mutation.invalidate();
      await reload(id === "order");
      setMessage(success);
    } catch (error) {
      mutation.invalidate();
      let recovered = false;
      if (!completed) {
        try { await reload(id === "order"); recovered = true; } catch { /* Keep mutations blocked until explicit reload succeeds. */ }
      }
      setMessage(`${id === "order" ? "Thứ tự có thể đã cập nhật một phần. " : ""}${completed
        ? "Đã lưu nhưng chưa tải lại được dữ liệu." : ingredientError(error)}${recovered ? "" : " Hãy tải lại dữ liệu trước khi thao tác tiếp."}`);
    } finally {
      setPendingId(null);
      mutation.end();
    }
  }

  function saveItem(item: EditableIngredient) {
    if (mutation.isBlocked() || item.conflicted) return;
    const fields = validateIngredient(item);
    setErrors((current) => ({ ...current, [item.clientId]: fields }));
    if (Object.keys(fields).length) { setMessage("Vui lòng kiểm tra các trường được đánh dấu."); return; }
    void run(item.clientId, "Đã lưu nguyên liệu.", async () => {
      const input = { name: item.name.trim(), quantity: item.quantity, unit: item.unit!.trim(),
        notes: item.notes?.trim() || null,
        sortOrder: model.server.find((saved) => saved.id === item.id)?.orderIndex ?? item.orderIndex, rowVersion };
      const saved = item.id ? await updateRecipeIngredient(recipeId, item.id, input)
        : await addRecipeIngredient(recipeId, input);
      acknowledge(saved, item.clientId);
    });
  }

  function saveOrder() {
    if (mutation.isBlocked()) return;
    const savedItems = items.filter((item) => item.id);
    const invalid = savedItems.find((item) => item.conflicted ||
      Object.keys(validateIngredient(model.server.find((saved) => saved.id === item.id)!)).length);
    if (invalid) { setMessage("Kiểm tra dữ liệu đã lưu và giải quyết xung đột bản nháp trước khi sắp xếp."); return; }
    if (!savedItems.length) return;
    void run("order", "Đã lưu thứ tự nguyên liệu.", async () => {
      let currentVersion = rowVersion;
      // shortcut: separate PUTs require reload on partial failure; add a batch API only if atomic ordering is required.
      for (const item of savedItems) {
        const committed = model.server.find((saved) => saved.id === item.id)!;
        if (committed.orderIndex === item.orderIndex) continue;
        const saved = await updateRecipeIngredient(recipeId, item.id, {
          name: committed.name, quantity: committed.quantity, unit: committed.unit!, notes: committed.notes,
          sortOrder: item.orderIndex, rowVersion: currentVersion,
        });
        currentVersion = saved.rowVersion;
        acknowledge(saved, item.clientId, true);
      }
    });
  }

  function removeItem(item: EditableIngredient) {
    if (mutation.isBlocked()) return;
    if (!item.id) { setItems((current) => current.filter((other) => other.clientId !== item.clientId)); return; }
    if (!window.confirm(`Xóa nguyên liệu "${item.name}"?`)) return;
    void run(item.clientId, "Đã xóa nguyên liệu.", async () => {
      const version = await deleteRecipeIngredient(recipeId, item.id, rowVersion);
      mutation.invalidate();
      if (version) onRowVersionChange(version);
      setModel((current) => ({ ...current,
        server: current.server.filter((saved) => saved.id !== item.id),
        items: current.items.filter((draft) => draft.clientId !== item.clientId) }));
    });
  }

  async function retryReload() {
    if (!mutation.begin(true)) return;
    try { await reload(true); setMessage("Đã tải lại dữ liệu. Bản nháp nội dung được giữ để kiểm tra."); }
    catch { setMessage("Chưa tải lại được dữ liệu. Kiểm tra kết nối và thử lại."); }
    finally { mutation.end(); }
  }

  return <section className="space-y-4" aria-busy={mutation.pending}>
    <div className="flex flex-wrap items-center justify-between gap-4">
      <div><h2 className="text-xl font-semibold">Nguyên liệu</h2>
        <p className="text-sm text-gray-600">Dùng nút Lên/Xuống hoặc kéo biểu tượng ⋮⋮ để sắp xếp.</p></div>
      <div className="flex gap-2">
        <button type="button" disabled={blocked} onClick={() => {
          if (mutation.isBlocked()) return;
          setItems((current) => [...current, { clientId: `new-${crypto.randomUUID()}`, id: "", name: "",
            quantity: null, unit: "", notes: null,
            orderIndex: Math.max(-1, ...current.map((item) => item.orderIndex), ...model.server.map((item) => item.orderIndex)) + 1 }]);
        }} className="rounded-lg border border-orange-600 px-4 py-2 font-semibold text-orange-700 disabled:opacity-50">Thêm nguyên liệu</button>
        <button type="button" disabled={blocked || !items.some((item) => item.id)} onClick={saveOrder}
          className="rounded-lg bg-orange-600 px-4 py-2 font-semibold text-white disabled:opacity-50">Lưu thứ tự</button>
      </div>
    </div>
    {message && <p role="status" className="text-sm text-orange-800">{message}</p>}
    {mutation.needsReload && <button type="button" disabled={mutation.pending} onClick={() => void retryReload()}
      className="rounded-lg border px-4 py-2">Tải lại dữ liệu</button>}
    {!items.length && <p className="rounded-lg border border-dashed p-4 text-gray-600">Chưa có nguyên liệu.</p>}
    <div className="space-y-3">{items.map((item, index) => <div key={item.clientId} draggable={!blocked}
      onDragStart={() => { if (!mutation.isBlocked()) setDraggedId(item.clientId); }}
      onDragOver={(event) => event.preventDefault()}
      onDrop={() => { if (draggedId) moveBy(draggedId, index); }}
      className="rounded-lg border border-gray-200 bg-gray-50 p-4">
      {item.conflicted && <div role="alert" className="mb-3 text-red-700">
        Nội dung đã thay đổi hoặc bị xóa ở phiên khác. Kiểm tra bản nháp trước khi áp dụng lại.
        <button type="button" disabled={blocked} className="ml-3 underline" onClick={() => {
          if (mutation.isBlocked()) return;
          setItems((current) => current.map((draft) => draft.clientId === item.clientId ? { ...draft, conflicted: false } : draft));
        }}>Giữ bản nháp</button>
        <button type="button" disabled={blocked} className="ml-3 underline" onClick={() => {
          if (mutation.isBlocked()) return;
          const saved = model.server.find((entry) => entry.id === item.id);
          setItems((current) => current.flatMap((draft) => draft.clientId !== item.clientId ? [draft]
            : saved ? [{ ...saved, clientId: item.clientId }] : []));
        }}>Dùng dữ liệu mới</button>
      </div>}
      <div className="flex items-start gap-3">
        <span className="cursor-grab pt-3 text-lg text-gray-500" aria-label={`Kéo nguyên liệu thứ ${index + 1}`}>⋮⋮</span>
        <div className="grid flex-1 gap-3 sm:grid-cols-2">
          {([ ["name", "Tên", 100], ["quantity", "Số lượng", undefined], ["unit", "Đơn vị", 50], ["notes", "Ghi chú", 500] ] as const).map(([field, label, maxLength]) =>
            <label key={field} className="space-y-1 text-sm font-medium">{label}
              <input className={inputClass} disabled={blocked} type={field === "quantity" ? "number" : "text"}
                step={field === "quantity" ? "any" : undefined} maxLength={maxLength}
                value={item[field] ?? ""} aria-invalid={!!errors[item.clientId]?.[field]}
                onChange={(event) => changeItem(item.clientId, field, event.target.value)} />
              {errors[item.clientId]?.[field] && <span className="block text-red-700">{errors[item.clientId][field]}</span>}
            </label>)}
        </div>
        <div className="flex shrink-0 flex-col gap-2">
          <button type="button" disabled={blocked || index === 0} onClick={() => moveBy(item.clientId, index - 1)}
            className="rounded-lg border px-3 py-2 disabled:opacity-40" aria-label={`Đưa ${item.name || `nguyên liệu ${index + 1}`} lên một vị trí`}>Lên</button>
          <button type="button" disabled={blocked || index === items.length - 1} onClick={() => moveBy(item.clientId, index + 1)}
            className="rounded-lg border px-3 py-2 disabled:opacity-40" aria-label={`Đưa ${item.name || `nguyên liệu ${index + 1}`} xuống một vị trí`}>Xuống</button>
          <button type="button" disabled={blocked || !!item.conflicted} onClick={() => saveItem(item)}
            className="rounded-lg bg-orange-600 px-3 py-2 text-white disabled:opacity-50">{pendingId === item.clientId ? "Đang lưu..." : "Lưu"}</button>
          <button type="button" disabled={blocked} onClick={() => removeItem(item)}
            className="rounded-lg border border-red-600 px-3 py-2 text-red-700 disabled:opacity-50">Xóa</button>
        </div>
      </div>
    </div>)}</div>
  </section>;
}
