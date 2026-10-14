"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";
import { getCategories } from "@/services/category-service";
import { getRecipeBySlug, updateRecipe } from "@/services/recipe-service";
import { buildUpdateRecipePayload, initialRecipeValues, mapRecipeError, nutritionFields, recipeValuesFromDetail,
  recipeFields, type FieldErrors, type RecipeField, type RecipeFormValues } from "@/services/recipe-form";
import type { Category } from "@/types/category";
import { ApiError } from "@/services/api-client";
import IngredientEditor from "./IngredientEditor";
import type { RecipeDetail, RecipeIngredient } from "@/types/recipe";
import { useSession } from "@/components/auth/useSession";
import { canManageIngredients } from "@/services/ingredient-form";
import { useRecipeMutation } from "./useRecipeMutation";

const labels: Record<RecipeField, string> = {
  title: "Tiêu đề", description: "Mô tả", categoryId: "Danh mục", prepTime: "Chuẩn bị (phút)",
  cookTime: "Nấu (phút)", servings: "Khẩu phần", difficulty: "Độ khó", instructions: "Hướng dẫn",
  calories: "Calories (kcal)", protein: "Protein (g)", carbs: "Carbs (g)", fat: "Fat (g)",
};
const inputClass = "w-full rounded-lg border border-gray-300 bg-white p-3 text-gray-900 focus:outline-orange-500";

export default function EditRecipeForm({ slug }: { slug: string }) {
  const [values, setValues] = useState<RecipeFormValues>(initialRecipeValues);
  const [categories, setCategories] = useState<Category[]>([]);
  const [recipeId, setRecipeId] = useState<string | null>(null);
  const [rowVersion, setRowVersion] = useState("");
  const [ingredients, setIngredients] = useState<RecipeIngredient[]>([]);
  const [state, setState] = useState<"loading" | "ready" | "missing" | "error">("loading");
  const [errors, setErrors] = useState<FieldErrors>({});
  const [message, setMessage] = useState("");
  const session = useSession();
  const mutation = useRecipeMutation();
  const pending = mutation.pending;
  const snapshot = useRef<RecipeDetail | null>(null);
  const baseline = useRef<RecipeFormValues | null>(null);
  const [authorId, setAuthorId] = useState("");

  useEffect(() => {
    let active = true;
    void Promise.all([getRecipeBySlug(slug), getCategories()]).then(([recipe, items]) => {
      if (!active) return;
      setRecipeId(recipe.id);
      setRowVersion(recipe.rowVersion);
      setValues(recipeValuesFromDetail(recipe));
      snapshot.current = recipe;
      baseline.current = recipeValuesFromDetail(recipe);
      setAuthorId(recipe.author.id);
      setIngredients(recipe.ingredients);
      setCategories(items);
      setState("ready");
    }).catch((error: unknown) => {
      if (active) setState(error instanceof ApiError && error.status === 404 ? "missing" : "error");
    });
    return () => { active = false; };
  }, [slug]);

  async function reloadIngredients() {
    const recipe = await getRecipeBySlug(slug);
    const incoming = recipeValuesFromDetail(recipe);
    const changed = baseline.current && recipeFields.some((field) => incoming[field] !== baseline.current![field]);
    snapshot.current = recipe;
    setRowVersion(recipe.rowVersion);
    setIngredients(recipe.ingredients);
    setAuthorId(recipe.author.id);
    if (changed) {
      mutation.invalidate();
      setMessage("Công thức đã thay đổi ở phiên khác. Bản nháp được giữ, nhưng chưa thể lưu. Tải dữ liệu mới để tránh ghi đè.");
    } else {
      baseline.current = incoming;
      mutation.recover();
    }
    return recipe;
  }

  async function reloadRecipeDraft() {
    if (!window.confirm("Tải dữ liệu mới sẽ bỏ bản nháp các trường công thức. Bản nháp nguyên liệu vẫn được giữ. Tiếp tục?")) return;
    if (!mutation.begin(true)) return;
    try {
      const recipe = await reloadIngredients();
      baseline.current = recipeValuesFromDetail(recipe);
      setValues(baseline.current);
      setErrors({});
      mutation.recover();
      setMessage("Đã tải dữ liệu mới. Hãy áp dụng lại thay đổi cần thiết.");
    } catch { setMessage("Không thể tải dữ liệu mới. Bản nháp vẫn được giữ; hãy kiểm tra kết nối."); }
    finally { mutation.end(); }
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!recipeId || !canManageIngredients(session, authorId) || !mutation.begin()) return;
    const result = buildUpdateRecipePayload(values, categories.map((category) => category.id), rowVersion);
    setErrors(result.errors ?? {});
    if (result.errors) { setMessage("Vui lòng kiểm tra các trường được đánh dấu."); mutation.end(); return; }
    setMessage("");
    try {
      const updated = await updateRecipe(recipeId, result.payload);
      baseline.current = recipeValuesFromDetail({ ...snapshot.current!, ...updated,
        category: { ...snapshot.current!.category, id: updated.categoryId } });
      setValues(baseline.current);
      setRowVersion(updated.rowVersion);
      mutation.invalidate();
      const reloaded = await reloadIngredients();
      const current = recipeValuesFromDetail(reloaded);
      if (recipeFields.every((field) => current[field] === baseline.current![field])) setMessage("Đã lưu thay đổi.");
    } catch (error) {
      const mapped = mapRecipeError(error);
      setErrors(mapped.fields);
      setMessage(mapped.message);
      mutation.invalidate();
      try { await reloadIngredients(); } catch { setMessage(`${mapped.message} Chưa tải lại được dữ liệu; hãy tải lại trước khi lưu tiếp.`); }
    } finally {
      mutation.end();
    }
  }

  function control(field: RecipeField, kind: "text" | "number" | "textarea" | "select" = "text") {
    const props = {
      id: field, name: field, value: values[field], className: inputClass,
      "aria-invalid": !!errors[field], "aria-describedby": errors[field] ? `${field}-error` : undefined,
      onChange: (event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) => {
        setValues((current) => ({ ...current, [field]: event.target.value }));
        setErrors((current) => ({ ...current, [field]: undefined }));
      },
    };
    return <div key={field} className="space-y-2">
      <label htmlFor={field} className="block font-medium">{labels[field]}</label>
      {kind === "textarea" ? <textarea {...props} rows={4} /> : kind === "select" ? <select {...props}>
        {field === "categoryId" ? <><option value="">Chọn danh mục</option>{categories.map((category) =>
          <option key={category.id} value={category.id}>{category.name}</option>)}</> :
          <><option value="1">Dễ</option><option value="2">Trung bình</option><option value="3">Khó</option></>}
      </select> : <input {...props} type={kind} min={kind === "number" ? (nutritionFields.includes(field as typeof nutritionFields[number]) ? 0 : 1) : undefined}
        step={nutritionFields.includes(field as typeof nutritionFields[number]) ? "any" : 1} />}
      {errors[field] && <p id={`${field}-error`} className="text-sm text-red-700">{errors[field]}</p>}
    </div>;
  }

  if (state === "loading") return <main className="p-8" role="status">Đang tải công thức...</main>;
  if (state === "missing") return <main className="p-8" role="alert">Không tìm thấy công thức.</main>;
  if (state === "error") return <main className="p-8" role="alert">Không thể tải công thức để chỉnh sửa.</main>;

  return <main className="mx-auto max-w-3xl px-6 py-10 text-gray-900">
    <h1 className="text-3xl font-bold">Chỉnh sửa công thức</h1>
    <form noValidate aria-busy={pending} onSubmit={submit} className="mt-6 space-y-6 rounded-xl border bg-white p-6">
      {message && <p role="alert" className="text-red-700">{message}</p>}
      {mutation.needsReload && <button type="button" disabled={pending} onClick={() => void reloadRecipeDraft()}
        className="rounded-lg border px-4 py-2">Tải dữ liệu mới (bỏ bản nháp công thức)</button>}
      <fieldset disabled={pending} className="space-y-5 disabled:opacity-60">
        {control("title")}{control("description", "textarea")}{control("categoryId", "select")}
        <div className="grid gap-4 sm:grid-cols-3">{control("prepTime", "number")}{control("cookTime", "number")}{control("servings", "number")}</div>
        {control("difficulty", "select")}{control("instructions", "textarea")}
        <fieldset className="space-y-4"><legend className="font-semibold">Dinh dưỡng</legend>
          <div className="grid gap-4 sm:grid-cols-2">{nutritionFields.map((field) => control(field, "number"))}</div>
        </fieldset>
        {canManageIngredients(session, authorId) && <IngredientEditor key={recipeId} recipeId={recipeId ?? ""}
          initialIngredients={ingredients} rowVersion={rowVersion} mutation={mutation}
          onRowVersionChange={setRowVersion} onReload={reloadIngredients} />}
        <button type="submit" disabled={pending || mutation.needsReload || !canManageIngredients(session, authorId)}
          className="rounded-lg bg-orange-600 px-5 py-3 font-semibold text-white disabled:opacity-50">
          {pending ? "Đang lưu..." : "Lưu thay đổi"}
        </button>
      </fieldset>
    </form>
  </main>;
}
