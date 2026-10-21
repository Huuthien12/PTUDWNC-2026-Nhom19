"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";
import RecipeImage from "@/components/shared/RecipeImage";
import ContentState from "@/components/shared/ContentState";
import { useSession } from "@/components/auth/useSession";
import { ApiError } from "@/services/api-client";
import { changeRecipeLifecycle, deleteRecipe, getMyRecipes, type RecipeLifecycleAction } from "@/services/recipe-service";
import { dashboardActions, lifecycleErrorMessage } from "@/services/recipe-dashboard";
import type { RecipeDashboardItem, RecipeDashboardPage } from "@/types/recipe";

const statusLabel = { 1: "Bản nháp", 2: "Đã xuất bản", 3: "Đã lưu trữ" } as const;
const pageSize = 12;
const conflictMessage = "Công thức đã thay đổi ở nơi khác. Dữ liệu mới nhất đã được tải lại.";

export default function AuthorRecipeDashboard() {
  const session = useSession();
  const [result, setResult] = useState<RecipeDashboardPage | null>(null);
  const [page, setPage] = useState(1);
  const [message, setMessage] = useState<string | null>(null);
  const [loadFailed, setLoadFailed] = useState(false);
  const [pendingId, setPendingId] = useState<string | null>(null);
  const latestRequestRef = useRef(0);

  const load = useCallback(async (targetPage: number, clearMessage = true) => {
    if (!session) return null;
    const requestId = ++latestRequestRef.current;
    if (clearMessage) setMessage(null);
    setLoadFailed(false);
    try {
      const next = await getMyRecipes(targetPage, pageSize);
      if (requestId !== latestRequestRef.current) return null;
      setResult(next);
      return next;
    } catch {
      if (requestId !== latestRequestRef.current) return null;
      setResult(null);
      setLoadFailed(true);
      setMessage("Không thể tải danh sách công thức.");
      return null;
    }
  }, [session]);

  useEffect(() => {
    const requestCounter = latestRequestRef;
    const timer = window.setTimeout(() => { void load(1); });
    return () => {
      window.clearTimeout(timer);
      requestCounter.current++;
    };
  }, [load]);

  async function goToPage(targetPage: number) {
    setPage(targetPage);
    await load(targetPage);
  }

  async function refreshAfterConflict() {
    if (await load(page, false)) setMessage(conflictMessage);
  }

  async function changeStatus(recipe: RecipeDashboardItem, action: RecipeLifecycleAction) {
    if (pendingId) return;
    if (action === "archive" && !window.confirm(`Lưu trữ công thức “${recipe.title}”? Hiện chưa có chức năng khôi phục.`)) return;
    setPendingId(recipe.id);
    setMessage(null);
    try {
      const updated = await changeRecipeLifecycle(recipe.id, action, recipe.rowVersion);
      setResult((current) => current ? { ...current, items: current.items.map((item) => item.id === recipe.id ? {
        ...item, status: updated.status, rowVersion: updated.rowVersion, publishedAt: updated.publishedAt,
      } : item) } : null);
    } catch (error) {
      if (error instanceof ApiError && error.status === 422) await refreshAfterConflict();
      else setMessage(lifecycleErrorMessage(error));
    } finally {
      setPendingId(null);
    }
  }

  async function remove(recipe: RecipeDashboardItem) {
    if (pendingId || !window.confirm(`Xóa công thức “${recipe.title}”?`)) return;
    setPendingId(recipe.id);
    setMessage(null);
    try {
      await deleteRecipe(recipe.id, recipe.rowVersion);
      const refreshed = await load(page);
      if (refreshed && page > 1 && (refreshed.items.length === 0 || page > refreshed.totalPages)) {
        const previousPage = Math.max(1, Math.min(page - 1, refreshed.totalPages || 1));
        setPage(previousPage);
        await load(previousPage);
      }
    } catch (error) {
      if (error instanceof ApiError && error.status === 422) await refreshAfterConflict();
      else setMessage(lifecycleErrorMessage(error));
    } finally {
      setPendingId(null);
    }
  }

  if (!result && !loadFailed) return <main className="cb-container cb-section" role="status">Đang tải công thức của bạn...</main>;
  if (!result) return <main className="cb-container cb-section" role="alert"><p>{message}</p><button className="mt-4 underline" onClick={() => void load(page)}>Thử lại</button></main>;

  const mutationPending = pendingId !== null;
  return <main className="cb-container cb-section">
    <header className="mb-8"><p className="text-sm font-semibold uppercase tracking-[0.18em] text-[#254b3b]">Không gian tác giả</p><h1 className="cb-section-title">Công thức của bạn</h1></header>
    {message && <p role="alert" className="mb-5 rounded-lg border border-amber-300 bg-amber-50 p-4 text-amber-900">{message}</p>}
{!result.items.length ? <ContentState headingAs="h2" title="Chưa có công thức" description="Bạn có thể tạo công thức đầu tiên của mình." href="/recipes/create" action="Tạo công thức" /> :
  <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">{result.items.map((recipe) => {

        const actions = dashboardActions(recipe.status);
        const pending = pendingId === recipe.id;
        return <article key={recipe.id} className="overflow-hidden rounded-[28px] border border-[#dfe1d1] bg-white shadow-[0_12px_30px_rgba(37,75,59,0.05)]">
          <RecipeImage src={recipe.mediumUrl ?? recipe.thumbnailUrl} mediumSrc={recipe.mediumUrl} thumbnailSrc={recipe.thumbnailUrl} title={recipe.title} />
          <div className="p-5 sm:p-6"><div className="flex items-start justify-between gap-3"><h2 className="font-serif text-2xl text-[#1d2b22]">{recipe.title}</h2><span className="rounded-full bg-[#f8f6f0] px-3 py-1 text-xs font-semibold text-[#254b3b]">{statusLabel[recipe.status]}</span></div>
            <p className="mt-2 text-sm text-[#5d655a]">{recipe.categoryName}{recipe.publishedAt ? ` · Xuất bản ${new Date(recipe.publishedAt).toLocaleDateString("vi-VN")}` : ""}</p>
            <div className="mt-5 flex flex-wrap gap-3"><Link className="rounded-lg border border-[#254b3b] px-3 py-2 text-sm font-semibold text-[#254b3b] focus:outline-none focus:ring-2 focus:ring-[#d7b354]" href={`/recipes/${recipe.slug}/edit`}>Chỉnh sửa</Link>
              {actions.map((action) => <button key={action} type="button" disabled={mutationPending} onClick={() => void changeStatus(recipe, action)} className="rounded-lg bg-[#254b3b] px-3 py-2 text-sm font-semibold text-white disabled:opacity-50 focus:outline-none focus:ring-2 focus:ring-[#d7b354]">{pending ? "Đang cập nhật..." : action === "publish" ? "Xuất bản" : action === "unpublish" ? "Gỡ xuất bản" : "Lưu trữ"}</button>)}
              <button type="button" disabled={mutationPending} onClick={() => void remove(recipe)} className="rounded-lg border border-red-700 px-3 py-2 text-sm font-semibold text-red-700 disabled:opacity-50 focus:outline-none focus:ring-2 focus:ring-red-400">{pending ? "Đang cập nhật..." : "Xóa"}</button></div>
          </div>
        </article>;
      })}</div>}
    {result.totalPages > 1 && <nav aria-label="Phân trang công thức" className="mt-8 flex items-center justify-center gap-4">
      <button type="button" disabled={page <= 1 || mutationPending} onClick={() => void goToPage(page - 1)} className="rounded-lg border px-4 py-2 disabled:opacity-50">Trang trước</button>
      <span>Trang {page} / {result.totalPages}</span>
      <button type="button" disabled={page >= result.totalPages || mutationPending} onClick={() => void goToPage(page + 1)} className="rounded-lg border px-4 py-2 disabled:opacity-50">Trang sau</button>
    </nav>}
  </main>;
}
