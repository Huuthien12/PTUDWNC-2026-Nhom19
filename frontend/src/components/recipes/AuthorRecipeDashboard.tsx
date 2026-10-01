"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import RecipeImage from "@/components/shared/RecipeImage";
import { useSession } from "@/components/auth/useSession";
import { ApiError } from "@/services/api-client";
import { changeRecipeLifecycle, deleteRecipe, getRecipeBySlug, getRecipes, type RecipeLifecycleAction } from "@/services/recipe-service";
import { dashboardActions, lifecycleErrorMessage } from "@/services/recipe-dashboard";
import type { RecipeDetail } from "@/types/recipe";

const statusLabel = { 1: "Bản nháp", 2: "Đã xuất bản", 3: "Đã lưu trữ" } as const;

export default function AuthorRecipeDashboard() {
  const session = useSession();
  const [recipes, setRecipes] = useState<RecipeDetail[] | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [pendingId, setPendingId] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!session) return;
    setMessage(null);
    try {
      const page = await getRecipes(1, 12);
      const details = await Promise.all(page.items.map((item) => getRecipeBySlug(item.slug)));
      setRecipes(details.filter((recipe) => recipe.author.id === session.user.id));
    } catch (error) {
      setRecipes(null);
      setMessage(lifecycleErrorMessage(error));
    }
  }, [session]);

  useEffect(() => {
    const timer = window.setTimeout(() => { void load(); });
    return () => window.clearTimeout(timer);
  }, [load]);

  async function changeStatus(recipe: RecipeDetail, action: RecipeLifecycleAction) {
    if (pendingId) return;
    setPendingId(recipe.id);
    setMessage(null);
    try {
      const updated = await changeRecipeLifecycle(recipe.id, action, recipe.rowVersion);
      setRecipes((current) => current?.map((item) => item.id === recipe.id ? {
        ...item, status: updated.status, rowVersion: updated.rowVersion, publishedAt: updated.publishedAt,
      } : item) ?? null);
    } catch (error) {
      setMessage(lifecycleErrorMessage(error));
      if (error instanceof ApiError && error.status === 422) await load();
    } finally {
      setPendingId(null);
    }
  }

  async function remove(recipe: RecipeDetail) {
    if (pendingId || !window.confirm(`Xóa công thức “${recipe.title}”?`)) return;
    setPendingId(recipe.id);
    setMessage(null);
    try {
      await deleteRecipe(recipe.id, recipe.rowVersion);
      setRecipes((current) => current?.filter((item) => item.id !== recipe.id) ?? null);
    } catch (error) {
      setMessage(lifecycleErrorMessage(error));
      if (error instanceof ApiError && error.status === 422) await load();
    } finally {
      setPendingId(null);
    }
  }

  if (!recipes && !message) return <main className="cb-container cb-section" role="status">Đang tải công thức của bạn...</main>;
  if (!recipes) return <main className="cb-container cb-section" role="alert"><p>{message}</p><button className="mt-4 underline" onClick={() => void load()}>Thử lại</button></main>;

  return <main className="cb-container cb-section">
    <header className="mb-8"><p className="text-sm font-semibold uppercase tracking-[0.18em] text-[#254b3b]">Không gian tác giả</p><h1 className="cb-section-title">Công thức của bạn</h1></header>
    {message && <p role="alert" className="mb-5 rounded-lg border border-amber-300 bg-amber-50 p-4 text-amber-900">{message}</p>}
    {!recipes.length ? <section className="cb-state"><h2 className="cb-section-title">Chưa có công thức</h2><p>Bạn có thể tạo công thức đầu tiên của mình.</p><Link className="mt-4 inline-block underline" href="/recipes/create">Tạo công thức</Link></section> :
      <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">{recipes.map((recipe) => {
        const image = recipe.images.find((item) => item.isPrimary) ?? recipe.images[0];
        const actions = dashboardActions(recipe.status);
        const pending = pendingId === recipe.id;
        return <article key={recipe.id} className="overflow-hidden rounded-[28px] border border-[#dfe1d1] bg-white shadow-[0_12px_30px_rgba(37,75,59,0.05)]">
          <RecipeImage src={image?.thumbnailUrl ?? image?.mediumUrl ?? image?.originalUrl ?? null} title={recipe.title} />
          <div className="p-5 sm:p-6"><div className="flex items-start justify-between gap-3"><h2 className="font-serif text-2xl text-[#1d2b22]">{recipe.title}</h2><span className="rounded-full bg-[#f8f6f0] px-3 py-1 text-xs font-semibold text-[#254b3b]">{statusLabel[recipe.status]}</span></div>
            <p className="mt-2 text-sm text-[#5d655a]">{recipe.category.name}{recipe.publishedAt ? ` · Xuất bản ${new Date(recipe.publishedAt).toLocaleDateString("vi-VN")}` : ""}</p>
            <div className="mt-5 flex flex-wrap gap-3"><Link className="rounded-lg border border-[#254b3b] px-3 py-2 text-sm font-semibold text-[#254b3b] focus:outline-none focus:ring-2 focus:ring-[#d7b354]" href={`/recipes/${recipe.slug}/edit`}>Chỉnh sửa</Link>
              {actions.map((action) => <button key={action} type="button" disabled={pending} onClick={() => void changeStatus(recipe, action)} className="rounded-lg bg-[#254b3b] px-3 py-2 text-sm font-semibold text-white disabled:opacity-50 focus:outline-none focus:ring-2 focus:ring-[#d7b354]">{pending ? "Đang cập nhật..." : action === "publish" ? "Xuất bản" : action === "unpublish" ? "Gỡ xuất bản" : "Lưu trữ"}</button>)}
              <button type="button" disabled={pending} onClick={() => void remove(recipe)} className="rounded-lg border border-red-700 px-3 py-2 text-sm font-semibold text-red-700 disabled:opacity-50 focus:outline-none focus:ring-2 focus:ring-red-400">{pending ? "Đang cập nhật..." : "Xóa"}</button></div>
          </div>
        </article>;
      })}</div>}
  </main>;
}
