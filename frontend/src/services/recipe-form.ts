import { ApiError } from "./api-client";
import type { CreateRecipeRequest, Difficulty, Nutrition } from "../types/recipe";

export const nutritionFields = ["calories", "protein", "carbs", "fat"] as const;
export const recipeFields = ["title", "description", "categoryId", "prepTime", "cookTime", "servings", "difficulty", "instructions", ...nutritionFields] as const;
export type RecipeField = typeof recipeFields[number];
export type RecipeFormValues = Record<RecipeField, string>;
export type FieldErrors = Partial<Record<RecipeField, string>>;
export const initialRecipeValues: RecipeFormValues = {
  title: "", description: "", categoryId: "", prepTime: "", cookTime: "", servings: "",
  difficulty: "1", instructions: "", calories: "", protein: "", carbs: "", fat: "",
};
export const recipeRoles = ["Author", "Admin"];
export const recipeCreatedDestination = "/categories?created=recipe";

export function buildRecipePayload(values: RecipeFormValues, categoryIds: string[]):
  { payload: CreateRecipeRequest; errors?: never } | { errors: FieldErrors; payload?: never } {
  const errors: FieldErrors = {};
  if (!values.title.trim() || values.title.length < 5 || values.title.length > 200)
    errors.title = "Tiêu đề cần từ 5 đến 200 ký tự và không được để trắng.";
  if (!categoryIds.includes(values.categoryId)) errors.categoryId = "Hãy chọn danh mục hiện có.";
  for (const field of ["prepTime", "cookTime", "servings"] as const) {
    const value = Number(values[field]);
    if (!/^\d+$/.test(values[field].trim()) || !Number.isInteger(value) || value <= 0 || value > 2147483647)
      errors[field] = "Nhập số nguyên dương trong phạm vi 1–2147483647.";
  }
  if (!["1", "2", "3"].includes(values.difficulty)) errors.difficulty = "Hãy chọn độ khó hợp lệ.";
  const nutrition: Nutrition = { calories: null, protein: null, carbs: null, fat: null };
  for (const field of nutritionFields) {
    const raw = values[field].trim();
    if (!raw) continue;
    const value = Number(raw);
    if (!/^(?:\d+(?:\.\d*)?|\.\d+)$/.test(raw) || !Number.isFinite(value) || value < 0)
      errors[field] = "Nhập số không âm, hoặc để trống nếu chưa có thông tin.";
    else nutrition[field] = value;
  }
  if (Object.keys(errors).length) return { errors };
  return { payload: {
    title: values.title, description: values.description, categoryId: values.categoryId,
    prepTime: Number(values.prepTime), cookTime: Number(values.cookTime), servings: Number(values.servings),
    difficulty: Number(values.difficulty) as Difficulty,
    ...(values.instructions ? { instructions: values.instructions } : {}),
    ...(nutritionFields.some((field) => nutrition[field] !== null) ? { nutrition } : {}),
  } };
}

export function mapRecipeError(error: unknown): { message: string; fields: FieldErrors } {
  const fields: FieldErrors = {};
  if (error instanceof ApiError) {
    if (error.status === 401) return { message: "Phiên đăng nhập không hợp lệ hoặc đã hết hạn. Vui lòng đăng nhập lại.", fields };
    if (error.status === 403) return { message: "Bạn không có quyền tạo công thức. Cần quyền Author hoặc Admin.", fields };
    if (error.status === 409) return {
      message: "Tên công thức tạo đường dẫn đã tồn tại. Hãy đổi tiêu đề và thử lại.",
      fields: { title: "Hãy chọn tiêu đề khác để tránh trùng đường dẫn." },
    };
    if (error.status === 400) {
      for (const path of error.validationFields) {
        const normalized = path.replace(/^\$\./, "").replace(/^request\./i, "").toLowerCase();
        const field = recipeFields.find((field) => normalized === field.toLowerCase() ||
          (nutritionFields.some((name) => name === field) && normalized === `nutrition.${field}`));
        if (field) fields[field] = field === "categoryId" ? "Danh mục không hợp lệ hoặc không còn tồn tại." :
          field === "title" ? "Kiểm tra tiêu đề (5–200 ký tự và phải tạo được đường dẫn)." : "Giá trị không hợp lệ. Vui lòng kiểm tra lại.";
      }
      return { message: "Dữ liệu chưa hợp lệ. Vui lòng kiểm tra các trường và thử lại.", fields };
    }
  }
  return { message: "Không thể xác nhận đã tạo công thức. Vui lòng kiểm tra kết nối trước khi thử lại.", fields };
}
