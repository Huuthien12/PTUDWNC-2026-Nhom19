"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import ContentState from "@/components/shared/ContentState";
import RecipeCard from "./RecipeCard";
import RecipePagination from "./RecipePagination";
import SearchPagination from "@/components/search/SearchPagination";
import RecipeFilters from "./RecipeFilters";
import { getCategories } from "@/services/category-service";
import { getRecipes } from "@/services/recipe-service";
import { searchRecipes } from "@/services/search-service";
import type { Category } from "@/types/category";
import type { RecipeListFilters, RecipePage } from "@/types/recipe";
import type { RecipeSearchPage } from "@/types/search";

export default function RecipeListClient({ page, query = "", filters: inputFilters = {} }: {
  page: number;
  query?: string;
  filters?: RecipeListFilters;
}) {
  const { categoryId, difficulty, maxCookTime, minServings, sortBy, sortOrder } = inputFilters;
  const filters = useMemo(() => ({ categoryId, difficulty, maxCookTime, minServings, sortBy, sortOrder }),
    [categoryId, difficulty, maxCookTime, minServings, sortBy, sortOrder]);
  const [recipes, setRecipes] = useState<RecipePage | RecipeSearchPage | null>(null);
  const [categories, setCategories] = useState<Category[]>([]);
  const [categoriesFailed, setCategoriesFailed] = useState(false);
  const [loadedKey, setLoadedKey] = useState("");
  const [failedKey, setFailedKey] = useState("");
  const [attempt, setAttempt] = useState(0);
  const normalizedQuery = query.trim();
  const hasActiveCriteria = Boolean(normalizedQuery || categoryId || difficulty !== undefined || maxCookTime !== undefined || minServings !== undefined);
  const filtersKey = JSON.stringify(filters);
  const requestKey = `${normalizedQuery}:${page}:${filtersKey}`;

  useEffect(() => {
    if (normalizedQuery.length >= 2) return;
    let active = true;
    void getCategories().then((value) => {
      if (active) { setCategories(value); setCategoriesFailed(false); }
    }).catch(() => {
      if (active) setCategoriesFailed(true);
    });
    return () => { active = false; };
  }, [normalizedQuery]);

  useEffect(() => {
    let active = true;
    const request = normalizedQuery.length >= 2
      ? searchRecipes(normalizedQuery, page)
      : getRecipes(page, 12, filters);
    void request.then((value) => {
      if (active) {
        setRecipes(value);
        setLoadedKey(requestKey);
        setFailedKey("");
      }
    }).catch(() => { if (active) setFailedKey(requestKey); });
    return () => { active = false; };
}, [normalizedQuery, page, requestKey, filters, attempt]);
const filterPanel = normalizedQuery.length >= 2 ? null : (
  <RecipeFilters key={`${filtersKey}:${categories.length}`} categories={categories} categoriesFailed={categoriesFailed} filters={filters} />
);
if (failedKey === requestKey) return <>{filterPanel}<ContentState headingAs="h2" title="Chưa thể tải công thức" description="Vui lòng thử lại sau."
  retryAction={<button type="button" className="cb-button cb-button-secondary" onClick={() => { setFailedKey(""); setLoadedKey(""); setAttempt(value => value + 1); }}>Thử tải lại</button>} /></>;
if (!recipes || loadedKey !== requestKey) return <>{filterPanel}<p role="status">Đang tải công thức...</p></>;
if (!recipes.items.length) return <>{filterPanel}<div className="cb-state"><h2 className="cb-section-title">{normalizedQuery ? "Không tìm thấy món ăn phù hợp" : "Chưa có công thức"}</h2><p>{normalizedQuery ? "Hãy thử từ khóa khác." : "Hãy đổi bộ lọc để xem thêm công thức."}</p>{hasActiveCriteria && <Link className="cb-button cb-button-secondary mt-4 inline-flex" href="/recipes">Xem tất cả món ăn</Link>}</div></>;
return <>{filterPanel}<div className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">{recipes.items.map((recipe) => <RecipeCard key={recipe.id} recipe={recipe} />)}</div>{normalizedQuery ? <SearchPagination query={normalizedQuery} currentPage={recipes.page} totalPages={recipes.totalPages} /> : <RecipePagination currentPage={recipes.page} totalPages={recipes.totalPages} filters={filters} />}</>;
}
