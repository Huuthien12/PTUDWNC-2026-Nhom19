import RecipeListClient from "@/components/recipes/RecipeListClient";
import type { RecipeDifficulty, RecipeListFilters } from "@/types/recipe";

const categoryIdPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const difficultyByValue: Record<string, RecipeDifficulty> = { "1": 1, "2": 2, "3": 3 };

export default async function RecipesPage({ searchParams }: { searchParams: Promise<{
  page?: string;
  q?: string;
  categoryId?: string;
  difficulty?: string;
  maxCookTime?: string;
  sort?: string;
  sortBy?: string;
  sortOrder?: string;
}> }) {
  const params = await searchParams;
  const rawQuery = params.q?.trim() ?? "";
  const query = rawQuery.length >= 2 ? rawQuery : "";
  const value = Number(params.page ?? "1");
  const page = Number.isInteger(value) && value > 0 ? value : 1;
  const difficulty = params.difficulty ? difficultyByValue[params.difficulty] : undefined;
  const rawCookTime = Number(params.maxCookTime);
  const maxCookTime = params.maxCookTime?.trim() && Number.isInteger(rawCookTime) && rawCookTime >= 0
    ? rawCookTime
    : undefined;
  const [requestedSortBy, requestedSortOrder] = (params.sort ?? "").split("-");
  const filters: RecipeListFilters = {
    ...(params.categoryId && categoryIdPattern.test(params.categoryId) ? { categoryId: params.categoryId } : {}),
    ...(difficulty !== undefined ? { difficulty } : {}),
    ...(maxCookTime !== undefined ? { maxCookTime } : {}),
    sortBy: params.sortBy === "title" || requestedSortBy === "title" ? "title" : "createdAt",
    sortOrder: params.sortOrder === "asc" || requestedSortOrder === "asc" ? "asc" : "desc",
  };
  return <main className="cb-container cb-section"><h1 className="cb-section-title">{query ? `Kết quả tìm kiếm cho "${query}"` : "Danh sách món ăn"}</h1><RecipeListClient page={page} query={query} filters={filters} /></main>;
}
