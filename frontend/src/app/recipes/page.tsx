import RecipeListClient from "@/components/recipes/RecipeListClient";

export default async function RecipesPage({ searchParams }: { searchParams: Promise<{ page?: string }> }) {
  const value = Number((await searchParams).page ?? "1");
  const page = Number.isInteger(value) && value > 0 ? value : 1;
  return <main className="cb-container cb-section"><h1 className="cb-section-title">Danh sách món ăn</h1><RecipeListClient page={page} /></main>;
}
