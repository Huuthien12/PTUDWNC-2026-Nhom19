"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { getCategories } from "@/services/category-service";
import { createRecipe } from "@/services/recipe-service";
import { buildRecipePayload, initialRecipeValues, mapRecipeError, nutritionFields, recipeCreatedDestination,
  type FieldErrors, type RecipeField } from "@/services/recipe-form";
import type { Category } from "@/types/category";

const labels: Record<RecipeField, string> = {
  title: "Tiêu đề", description: "Mô tả", categoryId: "Danh mục", prepTime: "Chuẩn bị (phút)",
  cookTime: "Nấu (phút)", servings: "Khẩu phần", difficulty: "Độ khó", instructions: "Hướng dẫn (tùy chọn)",
  calories: "Calories (kcal)", protein: "Protein (g)", carbs: "Carbs (g)", fat: "Fat (g)",
};
const inputClass = "w-full rounded-lg border border-gray-300 bg-white p-3 text-gray-900 focus:outline-orange-500";

export default function CreateRecipeForm() {
  const router = useRouter();
  const [values, setValues] = useState(initialRecipeValues);
  const [categories, setCategories] = useState<Category[]>([]);
  const [categoryState, setCategoryState] = useState<"loading" | "ready" | "error">("loading");
  const [reload, setReload] = useState(0);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [message, setMessage] = useState("");
  const [pending, setPending] = useState(false);
  const submitting = useRef(false);
  const alertRef = useRef<HTMLParagraphElement>(null);

  function reloadCategories() {
    setCategoryState("loading");
    setReload((value) => value + 1);
  }

  useEffect(() => {
    let active = true;
    void getCategories().then((items) => {
      if (active) {
        setCategories(items);
        // Keep the draft, but require a new selection if the selected category disappeared.
        setValues((current) => current.categoryId && !items.some((item) => item.id === current.categoryId)
          ? { ...current, categoryId: "" } : current);
        setCategoryState("ready");
      }
    }).catch(() => { if (active) setCategoryState("error"); });
    return () => { active = false; };
  }, [reload]);
  useEffect(() => { if (message) alertRef.current?.focus(); }, [message]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting.current || categoryState !== "ready" || !categories.length) return;
    const result = buildRecipePayload(values, categories.map((category) => category.id));
    setErrors(result.errors ?? {});
    if (result.errors) { setMessage("Vui lòng kiểm tra các trường được đánh dấu."); return; }
    submitting.current = true;
    setPending(true);
    setMessage("");
    try {
      await createRecipe(result.payload);
      router.replace(recipeCreatedDestination);
    } catch (error) {
      const mapped = mapRecipeError(error);
      setErrors(mapped.fields);
      setMessage(mapped.message);
      submitting.current = false;
      setPending(false);
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
      {kind === "textarea" ? <textarea {...props} rows={4} /> : kind === "select" ?
        <select {...props}>
          {field === "categoryId" ? <><option value="">Chọn danh mục</option>{categories.map((category) =>
            <option key={category.id} value={category.id}>{category.name}</option>)}</> :
            <><option value="1">Dễ</option><option value="2">Trung bình</option><option value="3">Khó</option></>}
        </select> : <input {...props} type={kind} min={kind === "number" ? (nutritionFields.some((name) => name === field) ? 0 : 1) : undefined}
          step={nutritionFields.some((name) => name === field) ? "any" : 1} />}
      {errors[field] && <p id={`${field}-error`} className="text-sm text-red-700">{errors[field]}</p>}
    </div>;
  }

  return <main className="mx-auto max-w-3xl px-6 py-10 text-gray-900">
    <h1 className="text-3xl font-bold">Tạo công thức</h1>
    <p className="my-4 text-gray-600">Công thức được lưu dưới dạng bản nháp. Sau khi lưu, bạn sẽ về trang danh mục; bản nháp chưa xuất hiện trong danh sách công khai.</p>
    {categoryState === "loading" && <p role="status">Đang tải danh mục...</p>}
    {categoryState === "error" && <div role="alert">Không tải được danh mục. <button type="button" className="underline"
      onClick={reloadCategories}>Thử lại</button></div>}
    {categoryState === "ready" && !categories.length && <p role="status">Chưa có danh mục. Vui lòng liên hệ quản trị viên để thêm danh mục trước khi tạo công thức.</p>}
    {categoryState === "ready" && <button type="button" disabled={pending} onClick={reloadCategories}
      className="mb-4 text-sm text-orange-700 underline disabled:opacity-50">Tải lại danh mục</button>}
    <form noValidate aria-busy={pending} onSubmit={submit} className="space-y-6 rounded-xl border bg-white p-6">
      {message && <p ref={alertRef} tabIndex={-1} role="alert" className="text-red-700">{message}</p>}
      <fieldset disabled={pending || categoryState !== "ready" || !categories.length} className="space-y-5 disabled:opacity-60">
        <legend className="sr-only">Thông tin công thức</legend>
        {control("title")}{control("description", "textarea")}{control("categoryId", "select")}
        <div className="grid gap-4 sm:grid-cols-3">{control("prepTime", "number")}{control("cookTime", "number")}{control("servings", "number")}</div>
        {control("difficulty", "select")}{control("instructions", "textarea")}
        <fieldset className="space-y-4"><legend className="font-semibold">Dinh dưỡng (tùy chọn)</legend>
          <p className="text-sm text-gray-600">Để trống nếu chưa có thông tin; 0 là giá trị thực.</p>
          <div className="grid gap-4 sm:grid-cols-2">{nutritionFields.map((field) => control(field, "number"))}</div>
        </fieldset>
        <button type="submit" className="rounded-lg bg-orange-600 px-5 py-3 font-semibold text-white disabled:opacity-50">
          {pending ? "Đang lưu..." : "Lưu bản nháp"}
        </button>
      </fieldset>
    </form>
  </main>;
}
