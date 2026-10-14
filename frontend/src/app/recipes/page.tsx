import RecipeListClient from "@/components/recipes/RecipeListClient";

export default async function RecipesPage({ searchParams }: { searchParams: Promise<{ page?: string; q?: string }> }) {
  const params = await searchParams;
  const rawQuery = params.q?.trim() ?? "";
  const query = rawQuery.length >= 2 ? rawQuery : "";
  const value = Number(params.page ?? "1");
  const page = Number.isInteger(value) && value > 0 ? value : 1;
  return <main className="cb-container cb-section"><h1 className="cb-section-title">{query ? `Kết quả tìm kiếm cho "${query}"` : "Danh sách món ăn"}</h1><RecipeListClient page={page} query={query} /></main>;
}
