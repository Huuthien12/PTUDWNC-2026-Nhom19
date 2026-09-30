"use client";
import { use, useEffect, useState } from "react";
import { ApiError } from "@/services/api-client";
import { getRecipeBySlug } from "@/services/recipe-service";
import type { RecipeDetail } from "@/types/recipe";
type PageProps = { params: Promise<{ slug: string }> };
export default function RecipeDetailPage({ params }: PageProps) {
  const { slug } = use(params);
  const [recipe, setRecipe] = useState<RecipeDetail | null>(null);
  const [state, setState] = useState<"loading" | "missing" | "error" | "ready">("loading");
  useEffect(() => { let active = true; void getRecipeBySlug(slug).then(value => { if (active) { setRecipe(value); setState("ready"); } }).catch((error: unknown) => { if (active) setState(error instanceof ApiError && error.status === 404 ? "missing" : "error"); }); return () => { active = false; }; }, [slug]);
  if (state === "loading") return <main className="p-8" role="status">Đang tải công thức...</main>;
  if (state === "missing") return <main className="p-8" role="alert">Không tìm thấy công thức.</main>;
  if (state === "error" || !recipe) return <main className="p-8" role="alert">Không thể tải công thức.</main>;
  const image = recipe.images.find(item => item.isPrimary) ?? recipe.images[0];
  return <main style={{ maxWidth: 1100, margin: "0 auto", padding: "40px 24px" }}><article><header><p>{recipe.category.name}</p><h1>{recipe.title}</h1><p>{recipe.description}</p><p>Chuẩn bị: {recipe.prepTime} phút · Nấu: {recipe.cookTime} phút · Khẩu phần: {recipe.servings}</p></header>{image && <img src={image.mediumUrl ?? image.originalUrl} alt={image.altText ?? recipe.title} style={{ width: "100%", maxHeight: 500, objectFit: "cover" }} />}<section><h2>Tác giả</h2><p>{recipe.author.fullName}</p></section><section><h2>Nguyên liệu</h2><ul>{recipe.ingredients.map(item => <li key={item.id}>{item.name}</li>)}</ul></section><section><h2>Các bước</h2><ol>{recipe.steps.map(item => <li key={item.id}><h3>{item.title ?? `Bước ${item.stepNumber}`}</h3><p>{item.description}</p></li>)}</ol></section><section><h2>Dinh dưỡng</h2><p>{recipe.nutrition ? `Calo: ${recipe.nutrition.calories ?? "-"} kcal` : "Chưa có thông tin dinh dưỡng."}</p></section><section><h2>Hướng dẫn chung</h2><p style={{ whiteSpace: "pre-line" }}>{recipe.instructions}</p></section></article></main>;
}
