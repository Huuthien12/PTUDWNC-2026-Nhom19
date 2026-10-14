"use client";

import { useEffect, useState } from "react";
import ContentState from "@/components/shared/ContentState";
import RecipeCard from "./RecipeCard";
import RecipePagination from "./RecipePagination";
import SearchPagination from "@/components/search/SearchPagination";
import { getRecipes } from "@/services/recipe-service";
import { searchRecipes } from "@/services/search-service";
import type { RecipePage } from "@/types/recipe";
import type { RecipeSearchPage } from "@/types/search";

export default function RecipeListClient({ page, query = "" }: { page: number; query?: string }) {
  const [recipes, setRecipes] = useState<RecipePage | RecipeSearchPage | null>(null);
  const [loadedKey, setLoadedKey] = useState("");
  const [failedKey, setFailedKey] = useState("");
  const normalizedQuery = query.trim();
  const requestKey = `${normalizedQuery}:${page}`;
  useEffect(() => {
    let active = true;
    const request = normalizedQuery.length >= 2 ? searchRecipes(normalizedQuery, page) : getRecipes(page);
    void request.then((value) => {
      if (active) {
        setRecipes(value);
        setLoadedKey(requestKey);
      }
    }).catch(() => { if (active) setFailedKey(requestKey); });
    return () => { active = false; };
  }, [normalizedQuery, page, requestKey]);
  if (failedKey === requestKey) return <ContentState title="Chưa thể tải công thức" description="Vui lòng thử lại sau." retry />;
  if (!recipes || loadedKey !== requestKey) return <p role="status">Đang tải công thức...</p>;
  if (!recipes.items.length) return <div className="cb-state"><h2 className="cb-section-title">{normalizedQuery ? "Không tìm thấy món ăn phù hợp" : "Chưa có công thức"}</h2><p>{normalizedQuery ? "Hãy thử từ khóa khác." : ""}</p></div>;
  return <><div className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">{recipes.items.map((recipe) => <RecipeCard key={recipe.id} recipe={recipe} />)}</div>{normalizedQuery ? <SearchPagination query={normalizedQuery} currentPage={recipes.page} totalPages={recipes.totalPages} /> : <RecipePagination currentPage={recipes.page} totalPages={recipes.totalPages} />}</>;
}
