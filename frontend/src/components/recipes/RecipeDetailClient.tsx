"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { ApiError } from "@/services/api-client";
import { getRecipeBySlug } from "@/services/recipe-service";
import type { RecipeDetail } from "@/types/recipe";
import { useSession } from "@/components/auth/useSession";
import { canManageIngredients } from "@/services/ingredient-form";
import RecipeImage from "@/components/shared/RecipeImage";

const difficulty: Record<number, string> = { 1: "Dễ", 2: "Trung bình", 3: "Khó" };
const format = (value: number | null) => value === null ? "—" : new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 1 }).format(value);

export default function RecipeDetailClient({ slug }: { slug: string }) {
  const [recipe, setRecipe] = useState<RecipeDetail | null>(null);
  const [loadedSlug, setLoadedSlug] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);
  const [prepared, setPrepared] = useState<Set<string>>(new Set());
  const [state, setState] = useState<"loading" | "missing" | "error" | "ready">("loading");
  const session = useSession();
  useEffect(() => { let active = true; void getRecipeBySlug(slug).then((value) => { if (active) { setRecipe(value); setLoadedSlug(slug); setState("ready"); } }).catch((error: unknown) => { if (active) { setLoadedSlug(slug); setState(error instanceof ApiError && error.status === 404 ? "missing" : "error"); } }); return () => { active = false; }; }, [slug, attempt]);
  if (loadedSlug !== slug || state === "loading") return <main className="cb-container cb-section"><p role="status">Đang tải công thức...</p></main>;
  if (state === "missing") return <main className="cb-container cb-section"><p role="alert">Không tìm thấy công thức.</p></main>;
  if (state === "error" || !recipe) return <main className="cb-container cb-section"><p role="alert">Không thể tải công thức. <button className="cb-text-link" onClick={() => { setState("loading"); setAttempt((value) => value + 1); }}>Thử lại</button></p></main>;
  const image = recipe.images.find((item) => item.isPrimary) ?? recipe.images[0];
  const canEditRecipe = canManageIngredients(session, recipe.author.id);
  const ingredients = [...recipe.ingredients].sort((a, b) => a.orderIndex - b.orderIndex);
  const steps = [...recipe.steps].sort((a, b) => a.stepNumber - b.stepNumber);
  const nutrition = recipe.nutrition && Object.values(recipe.nutrition).some((value) => value !== null);
  return <main className="cb-recipe-detail"><article className="cb-container cb-section">
    <nav className="cb-breadcrumb" aria-label="Điều hướng đường dẫn"><Link href="/">Trang chủ</Link><span aria-hidden="true">/</span><Link href={`/categories/${recipe.category.slug}`}>{recipe.category.name}</Link><span aria-hidden="true">/</span><span aria-current="page">{recipe.title}</span></nav>
    <header className="cb-recipe-detail-header"><div><p className="cb-recipe-detail-category">{recipe.category.name} · {difficulty[recipe.difficulty] ?? "Chưa xác định"}</p><h1 className="cb-title">{recipe.title}</h1><p className="cb-lead">{recipe.description}</p><p className="cb-recipe-author">Công thức bởi <strong>{recipe.author.fullName}</strong></p></div>{canEditRecipe && <Link href={`/recipes/${encodeURIComponent(recipe.slug)}/edit`} className="cb-button">Chỉnh sửa công thức</Link>}</header>
    <dl className="cb-recipe-facts"><div><dt>Chuẩn bị</dt><dd>{recipe.prepTime} phút</dd></div><div><dt>Chế biến</dt><dd>{recipe.cookTime} phút</dd></div><div><dt>Khẩu phần</dt><dd>{recipe.servings} người</dd></div></dl>
    <RecipeImage className="cb-recipe-visual--detail" src={image?.originalUrl} mediumSrc={image?.mediumUrl} thumbnailSrc={image?.thumbnailUrl} title={recipe.title} alt={image?.altText} />
    <div className="cb-recipe-detail-grid"><section aria-labelledby="ingredients-heading" className="cb-detail-surface"><h2 id="ingredients-heading" className="cb-section-title">Nguyên liệu</h2>{ingredients.length ? <ul className="cb-ingredients">{ingredients.map((item) => <li key={item.id}><label><input type="checkbox" checked={prepared.has(item.id)} onChange={() => setPrepared((current) => { const next = new Set(current); if (next.has(item.id)) next.delete(item.id); else next.add(item.id); return next; })} /><span>{item.name}</span></label><span>{item.quantity === null ? "" : format(item.quantity)}{item.unit ? ` ${item.unit}` : ""}{item.notes ? ` · ${item.notes}` : ""}</span></li>)}</ul> : <p className="cb-empty-copy">Chưa có nguyên liệu được thêm.</p>}</section>
    <section aria-labelledby="nutrition-heading" className="cb-nutrition"><h2 id="nutrition-heading" className="cb-section-title">Dinh dưỡng</h2>{nutrition ? <dl>{[["Calories", recipe.nutrition!.calories, "kcal"], ["Protein", recipe.nutrition!.protein, "g"], ["Carbs", recipe.nutrition!.carbs, "g"], ["Chất béo", recipe.nutrition!.fat, "g"]].map(([label, value, unit]) => <div key={String(label)}><dt>{label}</dt><dd>{format(value as number | null)} <small>{unit}</small></dd></div>)}</dl> : <p className="cb-empty-copy">Chưa có thông tin dinh dưỡng.</p>}</section></div>
    <section className="cb-detail-section" aria-labelledby="steps-heading"><h2 id="steps-heading" className="cb-section-title">Cách thực hiện</h2>{steps.length ? <ol className="cb-recipe-steps">{steps.map((step) => <li key={step.id}><div className="cb-step-number">{step.stepNumber}</div><div><h3>{step.title || `Bước ${step.stepNumber}`}</h3><p>{step.description}</p>{step.timerMinutes !== null && <p className="cb-step-time">Thời gian: {step.timerMinutes} phút</p>}{step.imageUrl && <RecipeImage src={step.imageUrl} title={step.title || `Bước ${step.stepNumber}`} alt={step.title} />}</div></li>)}</ol> : <p className="cb-empty-copy">Chưa có bước chế biến.</p>}</section>
    {recipe.instructions && <section className="cb-detail-section" aria-labelledby="notes-heading"><h2 id="notes-heading" className="cb-section-title">Ghi chú</h2><p className="cb-instructions">{recipe.instructions}</p></section>}
  </article></main>;
}
