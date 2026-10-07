"use client";

import { useState } from "react";
import {
  addRecipeIngredient,
  deleteRecipeIngredient,
  updateRecipeIngredient,
  type RecipeIngredientInput,
} from "@/services/ingredient-service";
import type { RecipeIngredient } from "@/types/recipe";

type EditableIngredient = RecipeIngredient & { clientId: string };

interface IngredientEditorProps {
  recipeId: string;
  initialIngredients: RecipeIngredient[];
  rowVersion: string;
  onRowVersionChange: (rowVersion: string) => void;
  onReload: () => Promise<void>;
}

const inputClass = "w-full rounded-lg border border-gray-300 bg-white p-3 text-gray-900 focus:outline-orange-500";
const emptyIngredient = (): EditableIngredient => ({
  clientId: `new-${Date.now()}-${Math.random().toString(36).slice(2)}`,
  id: "",
  name: "",
  quantity: null,
  unit: "",
  notes: null,
  orderIndex: 0,
});

function toEditable(items: RecipeIngredient[]): EditableIngredient[] {
  return [...items]
    .sort((left, right) => left.orderIndex - right.orderIndex)
    .map((item) => ({ ...item, clientId: item.id }));
}

export default function IngredientEditor({
  recipeId,
  initialIngredients,
  rowVersion,
  onRowVersionChange,
  onReload,
}: IngredientEditorProps) {
  const [items, setItems] = useState<EditableIngredient[]>(() => toEditable(initialIngredients));
  const [draggedId, setDraggedId] = useState<string | null>(null);
  const [pendingId, setPendingId] = useState<string | null>(null);
  const [message, setMessage] = useState("");

  function changeItem(clientId: string, field: "name" | "quantity" | "unit" | "notes", value: string) {
    setItems((current) => current.map((item) => item.clientId === clientId
      ? { ...item, [field]: field === "quantity" ? (value === "" ? null : Number(value)) : value }
      : item));
  }

  function moveItem(targetId: string) {
    if (!draggedId || draggedId === targetId) return;
    setItems((current) => {
      const from = current.findIndex((item) => item.clientId === draggedId);
      const to = current.findIndex((item) => item.clientId === targetId);
      if (from < 0 || to < 0) return current;
      const next = [...current];
      const [moved] = next.splice(from, 1);
      next.splice(to, 0, moved);
      return next.map((item, index) => ({ ...item, orderIndex: index }));
    });
    setDraggedId(null);
  }

  function moveBy(clientId: string, direction: -1 | 1) {
    setItems((current) => {
      const index = current.findIndex((item) => item.clientId === clientId);
      const targetIndex = index + direction;
      if (index < 0 || targetIndex < 0 || targetIndex >= current.length) return current;
      const next = [...current];
      [next[index], next[targetIndex]] = [next[targetIndex], next[index]];
      return next.map((item, itemIndex) => ({ ...item, orderIndex: itemIndex }));
    });
  }

  async function saveItem(item: EditableIngredient) {
    if (!item.name.trim() || !(item.unit ?? "").trim()) {
      setMessage("Tên và đơn vị nguyên liệu không được để trống.");
      return;
    }
    setPendingId(item.clientId);
    setMessage("");
    const input: RecipeIngredientInput = {
      name: item.name.trim(),
      quantity: item.quantity,
      unit: (item.unit ?? "").trim(),
      notes: item.notes?.trim() || null,
      sortOrder: item.orderIndex,
      rowVersion,
    };
    try {
      const saved = item.id
        ? await updateRecipeIngredient(recipeId, item.id, input)
        : await addRecipeIngredient(recipeId, input);
      onRowVersionChange(saved.rowVersion);
      await onReload();
      setMessage("Đã lưu nguyên liệu.");
    } catch {
      setMessage("Không thể lưu nguyên liệu. Dữ liệu có thể đã thay đổi, vui lòng tải lại.");
    } finally {
      setPendingId(null);
    }
  }

  async function saveOrder() {
    const savedItems = items.filter((item) => item.id);
    if (!savedItems.length) return;
    setPendingId("order");
    setMessage("");
    let currentRowVersion = rowVersion;
    try {
      for (const item of savedItems) {
        const saved = await updateRecipeIngredient(recipeId, item.id, {
          name: item.name.trim(),
          quantity: item.quantity,
          unit: (item.unit ?? "").trim(),
          notes: item.notes?.trim() || null,
          sortOrder: item.orderIndex,
          rowVersion: currentRowVersion,
        });
        currentRowVersion = saved.rowVersion;
      }
      onRowVersionChange(currentRowVersion);
      await onReload();
      setMessage("Đã lưu thứ tự nguyên liệu.");
    } catch {
      setMessage("Không thể lưu thứ tự. Dữ liệu có thể đã thay đổi, vui lòng tải lại.");
    } finally {
      setPendingId(null);
    }
  }

  async function removeItem(item: EditableIngredient) {
    if (!item.id) {
      setItems((current) => current.filter((candidate) => candidate.clientId !== item.clientId));
      return;
    }
    if (!window.confirm(`Xóa nguyên liệu "${item.name}"?`)) return;
    setPendingId(item.clientId);
    setMessage("");
    try {
      await deleteRecipeIngredient(recipeId, item.id, rowVersion);
      await onReload();
      setMessage("Đã xóa nguyên liệu.");
    } catch {
      setMessage("Không thể xóa nguyên liệu. Dữ liệu có thể đã thay đổi, vui lòng tải lại.");
    } finally {
      setPendingId(null);
    }
  }

  return (
    <section className="space-y-4">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-semibold">Nguyên liệu</h2>
          <p className="text-sm text-gray-600">Dùng nút Lên/Xuống hoặc kéo biểu tượng ⋮⋮ để sắp xếp.</p>
        </div>
        <div className="flex gap-2">
          <button type="button" disabled={pendingId !== null}
            onClick={() => setItems((current) => [...current, { ...emptyIngredient(), orderIndex: current.length }])}
            className="rounded-lg border border-orange-600 px-4 py-2 font-semibold text-orange-700 disabled:opacity-50">
            Thêm nguyên liệu
          </button>
          <button type="button" disabled={pendingId !== null || !items.some((item) => item.id)}
            onClick={() => void saveOrder()}
            className="rounded-lg bg-orange-600 px-4 py-2 font-semibold text-white disabled:opacity-50">
            Lưu thứ tự
          </button>
        </div>
      </div>
      {message && <p role="status" className="text-sm text-orange-800">{message}</p>}
      {!items.length && <p className="rounded-lg border border-dashed p-4 text-gray-600">Chưa có nguyên liệu.</p>}
      <div className="space-y-3">
        {items.map((item, index) => (
          <div key={item.clientId} draggable={pendingId === null}
            onDragStart={() => setDraggedId(item.clientId)} onDragOver={(event) => event.preventDefault()}
            onDrop={() => moveItem(item.clientId)}
            className="rounded-lg border border-gray-200 bg-gray-50 p-4">
            <div className="flex items-start gap-3">
              <span className="cursor-grab pt-3 text-lg text-gray-500" aria-label={`Kéo nguyên liệu thứ ${index + 1}`}>⋮⋮</span>
              <div className="grid flex-1 gap-3 sm:grid-cols-2">
                <label className="space-y-1 text-sm font-medium">Tên
                  <input className={inputClass} value={item.name} onChange={(event) => changeItem(item.clientId, "name", event.target.value)} />
                </label>
                <label className="space-y-1 text-sm font-medium">Số lượng
                  <input className={inputClass} type="number" min="0" step="any" value={item.quantity ?? ""}
                    onChange={(event) => changeItem(item.clientId, "quantity", event.target.value)} />
                </label>
                <label className="space-y-1 text-sm font-medium">Đơn vị
                  <input className={inputClass} value={item.unit ?? ""} onChange={(event) => changeItem(item.clientId, "unit", event.target.value)} />
                </label>
                <label className="space-y-1 text-sm font-medium">Ghi chú
                  <input className={inputClass} value={item.notes ?? ""} onChange={(event) => changeItem(item.clientId, "notes", event.target.value)} />
                </label>
              </div>
              <div className="flex shrink-0 flex-col gap-2">
                <button type="button" disabled={pendingId !== null || index === 0}
                  onClick={() => moveBy(item.clientId, -1)}
                  className="rounded-lg border border-gray-300 px-3 py-2 text-sm font-semibold text-gray-700 disabled:opacity-40"
                  aria-label={`Đưa ${item.name || `nguyên liệu ${index + 1}`} lên một vị trí`}>
                  Lên
                </button>
                <button type="button" disabled={pendingId !== null || index === items.length - 1}
                  onClick={() => moveBy(item.clientId, 1)}
                  className="rounded-lg border border-gray-300 px-3 py-2 text-sm font-semibold text-gray-700 disabled:opacity-40"
                  aria-label={`Đưa ${item.name || `nguyên liệu ${index + 1}`} xuống một vị trí`}>
                  Xuống
                </button>
                <button type="button" disabled={pendingId !== null} onClick={() => void saveItem(item)}
                  className="rounded-lg bg-orange-600 px-3 py-2 text-sm font-semibold text-white disabled:opacity-50">
                  {pendingId === item.clientId ? "Đang lưu..." : "Lưu"}
                </button>
                <button type="button" disabled={pendingId !== null} onClick={() => void removeItem(item)}
                  className="rounded-lg border border-red-600 px-3 py-2 text-sm font-semibold text-red-700 disabled:opacity-50">
                  Xóa
                </button>
              </div>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
