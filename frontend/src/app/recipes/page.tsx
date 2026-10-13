import RecipeListClient from "@/components/recipes/RecipeListClient";

export default async function RecipesPage({ searchParams }: { searchParams: Promise<{ page?: string; q?: string }> }) {
  const params = await searchParams;
  const query = params.q?.trim() ?? "";
  const value = Number(params.page ?? "1");
  const page = Number.isInteger(value) && value > 0 ? value : 1;
  return <main className="cb-container cb-section"><h1 className="cb-section-title">{query ? `Kết quả tìm kiếm cho "${query}"` : "Danh sách món ăn"}</h1><RecipeListClient page={page} query={query} /></main>;
}
