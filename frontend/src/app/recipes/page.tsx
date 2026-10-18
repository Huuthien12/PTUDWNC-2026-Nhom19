import RecipeListClient from "@/components/recipes/RecipeListClient";
import type { RecipeDifficulty, RecipeListFilters } from "@/types/recipe";

const categoryIdPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export default async function RecipesPage({ searchParams }: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = Object.fromEntries(Object.entries(await searchParams)
    .filter((entry): entry is [string, string] => typeof entry[1] === "string"));
  const rawQuery = params.q?.trim() ?? "";
  const query = rawQuery.length >= 2 ? rawQuery : "";
  const value = Number(params.page ?? "1");
  const page = Number.isInteger(value) && value > 0 ? value : 1;
  const difficulty = ["1", "2", "3"].includes(params.difficulty)
    ? Number(params.difficulty) as RecipeDifficulty : undefined;
  const rawCookTime = Number(params.maxCookTime);
  const maxCookTime = params.maxCookTime?.trim() && Number.isInteger(rawCookTime) && rawCookTime >= 0
    ? rawCookTime
    : undefined;
  const [requestedSortBy, requestedSortOrder] = (params.sort ?? "").split("-");
  const rawServings = Number(params.minServings);
  const filters: RecipeListFilters = {
    ...(params.categoryId && categoryIdPattern.test(params.categoryId) ? { categoryId: params.categoryId } : {}),
    ...(difficulty !== undefined ? { difficulty } : {}),
    ...(maxCookTime !== undefined ? { maxCookTime } : {}),
    ...(Number.isInteger(rawServings) && rawServings >= 1 ? { minServings: rawServings } : {}),
    sortBy: (params.sortBy ?? requestedSortBy) === "title" ? "title" : "createdAt",
    sortOrder: (params.sortOrder ?? requestedSortOrder) === "asc" ? "asc" : "desc",
  };
  return <main className="cb-container cb-section"><h1 className="cb-section-title">{query ? `Kết quả tìm kiếm cho "${query}"` : "Danh sách món ăn"}</h1><RecipeListClient page={page} query={query} filters={filters} /></main>;
}
