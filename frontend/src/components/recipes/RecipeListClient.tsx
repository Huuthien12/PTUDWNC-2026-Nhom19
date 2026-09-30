"use client";

import { useEffect, useState } from "react";
import ContentState from "@/components/shared/ContentState";
import RecipeCard from "./RecipeCard";
import RecipePagination from "./RecipePagination";
import { getRecipes } from "@/services/recipe-service";
import type { RecipePage } from "@/types/recipe";

export default function RecipeListClient({ page }: { page: number }) {
  const [recipes, setRecipes] = useState<RecipePage | null>(null);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    let active = true;
    void getRecipes(page).then((value) => { if (active) setRecipes(value); })
      .catch(() => { if (active) setFailed(true); });
    return () => { active = false; };
  }, [page]);
  if (failed) return <ContentState title="Chưa thể tải công thức" description="Vui lòng thử lại sau." retry />;
  if (!recipes) return <p role="status">Đang tải công thức...</p>;
  if (!recipes.items.length) return <div className="cb-state"><h2 className="cb-section-title">Chưa có công thức</h2></div>;
  return <><div className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">{recipes.items.map((recipe) => <RecipeCard key={recipe.id} recipe={recipe} />)}</div><RecipePagination currentPage={recipes.page} totalPages={recipes.totalPages} /></>;
}
