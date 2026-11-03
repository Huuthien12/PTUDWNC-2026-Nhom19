"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ApiError } from "@/services/api-client";
import { getRecipeBySlug } from "@/services/recipe-service";
import type { RecipeDetail } from "@/types/recipe";
import IngredientEditor from "@/components/recipes/IngredientEditor";
import { useSession } from "@/components/auth/useSession";
import { canManageIngredients } from "@/services/ingredient-form";
import { useRecipeMutation } from "@/components/recipes/useRecipeMutation";
import RecipeImage from "@/components/shared/RecipeImage";

export default function RecipeDetailClient({ slug }: { slug: string }) {
  const [recipe, setRecipe] = useState<RecipeDetail | null>(null);
  const [loadedSlug, setLoadedSlug] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);
  const [state, setState] = useState<"loading" | "missing" | "error" | "ready">("loading");
  const session = useSession();
  const mutation = useRecipeMutation();

  useEffect(() => {
    let active = true;
    void getRecipeBySlug(slug).then((value) => {
      if (active) { setRecipe(value); setLoadedSlug(slug); setState("ready"); }
    }).catch((error: unknown) => {
      if (active) { setLoadedSlug(slug); setState(error instanceof ApiError && error.status === 404 ? "missing" : "error"); }
    });
    return () => { active = false; };
  }, [slug, attempt]);

  if (loadedSlug !== slug || state === "loading") return <main className="p-8" role="status">Đang tải công thức...</main>;
  if (state === "missing") return <main className="p-8" role="alert">Không tìm thấy công thức.</main>;
  if (state === "error" || !recipe) return <main className="p-8" role="alert">Không thể tải công thức. <button onClick={() => { setState("loading"); setAttempt((value) => value + 1); }}>Thử lại</button></main>;

  const image = recipe.images.find((item) => item.isPrimary) ?? recipe.images[0];
  const canEditRecipe = canManageIngredients(session, recipe.author.id);
  async function reloadRecipe() {
    const current = await getRecipeBySlug(slug);
    setRecipe(current);
    mutation.recover();
    return current;
  }

  return <main style={{ maxWidth: 1100, margin: "0 auto", padding: "40px 24px" }}><article><header><p>{recipe.category.name}</p><div className="flex flex-wrap items-center justify-between gap-4"><h1>{recipe.title}</h1>{canEditRecipe && <Link href={`/recipes/${encodeURIComponent(recipe.slug)}/edit`} className="rounded-lg bg-orange-600 px-4 py-2 font-semibold text-white">Chỉnh sửa công thức</Link>}</div><p>{recipe.description}</p><p>Chuẩn bị: {recipe.prepTime} phút · Nấu: {recipe.cookTime} phút · Khẩu phần: {recipe.servings}</p></header>{image && <RecipeImage className="cb-recipe-visual--detail" src={image?.originalUrl} mediumSrc={image?.mediumUrl} thumbnailSrc={image?.thumbnailUrl} title={recipe.title} alt={image?.altText} />}<section><h2>Tác giả</h2><p>{recipe.author.fullName}</p></section><section><h2>Nguyên liệu</h2><ul>{recipe.ingredients.map((item) => <li key={item.id}>{item.name}</li>)}</ul>{canEditRecipe && <IngredientEditor key={recipe.id} recipeId={recipe.id} initialIngredients={recipe.ingredients} rowVersion={recipe.rowVersion} mutation={mutation} onRowVersionChange={(rowVersion) => setRecipe((current) => current ? { ...current, rowVersion } : current)} onReload={reloadRecipe} />}</section><section><h2>Các bước</h2><ol>{recipe.steps.map((item) => <li key={item.id}><h3>{item.title ?? `Bước ${item.stepNumber}`}</h3><p>{item.description}</p></li>)}</ol></section><section><h2>Dinh dưỡng</h2><p>{recipe.nutrition ? `Calo: ${recipe.nutrition.calories ?? "-"} kcal` : "Chưa có thông tin dinh dưỡng."}</p></section><section><h2>Hướng dẫn chung</h2><p style={{ whiteSpace: "pre-line" }}>{recipe.instructions}</p></section></article></main>;
}
