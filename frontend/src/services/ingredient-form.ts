import { ApiError } from "./api-client";
import type { AuthSession } from "../types/auth";
import type { RecipeIngredient } from "../types/recipe";

export const ingredientFields = ["name", "quantity", "unit", "notes"] as const;
export type IngredientField = typeof ingredientFields[number];
export type IngredientErrors = Partial<Record<IngredientField, string>>;
export type EditableIngredient = RecipeIngredient & { clientId: string; conflicted?: boolean };

export function canManageIngredients(session: AuthSession | null | undefined, authorId: string) {
  return !!session && (session.user.roles.includes("Admin") ||
    (session.user.roles.includes("Author") && session.user.id === authorId));
}

export function validateIngredient(item: RecipeIngredient): IngredientErrors {
  const errors: IngredientErrors = {};
  if (!item.name.trim() || item.name.length > 100) errors.name = "Tên cần từ 1 đến 100 ký tự.";
  if (!item.unit?.trim() || item.unit.length > 50) errors.unit = "Đơn vị bắt buộc, tối đa 50 ký tự.";
  if ((item.notes?.length ?? 0) > 500) errors.notes = "Ghi chú tối đa 500 ký tự.";
  if (item.quantity !== null && (!Number.isFinite(item.quantity) || item.quantity <= 0))
    errors.quantity = "Số lượng phải lớn hơn 0 hoặc để trống.";
  return errors;
}

export function ingredientError(error: unknown): string {
  if (error instanceof ApiError) {
    switch (error.status) {
      case 400: return "Dữ liệu nguyên liệu chưa hợp lệ. Kiểm tra tên, số lượng, đơn vị và ghi chú.";
      case 401: return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
      case 403: return "Bạn không có quyền sửa nguyên liệu của công thức này.";
      case 404: return "Công thức hoặc nguyên liệu không còn tồn tại. Vui lòng tải lại.";
      case 422: return "Công thức đã thay đổi ở phiên khác. Đã tải lại dữ liệu; hãy kiểm tra bản nháp trước khi lưu lại.";
    }
  }
  return "Không thể xác nhận kết quả. Vui lòng kiểm tra kết nối và tải lại trước khi thử lại.";
}

export function reconcileIngredients(drafts: EditableIngredient[], previous: RecipeIngredient[], incoming: RecipeIngredient[], resetOrder = false): EditableIngredient[] {
  const next = incoming.map((saved) => {
    const draft = drafts.find((item) => item.id === saved.id);
    const old = previous.find((item) => item.id === saved.id);
    if (!draft || !old) return { ...saved, clientId: saved.id };
    const dirty = ingredientFields.some((field) => draft[field] !== old[field]);
    const changed = ingredientFields.some((field) => saved[field] !== old[field]);
    return {
      ...saved, ...(dirty ? { name: draft.name, quantity: draft.quantity, unit: draft.unit, notes: draft.notes } : {}),
      clientId: draft.clientId, conflicted: draft.conflicted || (dirty && changed),
      orderIndex: !resetOrder && draft.orderIndex !== old.orderIndex && saved.orderIndex === old.orderIndex
        ? draft.orderIndex : saved.orderIndex,
    };
  });
  for (const draft of drafts) {
    if (!draft.id) next.push(draft);
    else if (!incoming.some((item) => item.id === draft.id)) {
      const old = previous.find((item) => item.id === draft.id);
      if (old && ingredientFields.some((field) => draft[field] !== old[field]))
        next.push({ ...draft, id: "", conflicted: true });
    }
  }
  return next.sort((a, b) => a.orderIndex - b.orderIndex);
}
