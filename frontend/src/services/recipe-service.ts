import type { RecipeDetail } from "@/types/recipe";

const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5062";

export async function getRecipeBySlug(
  slug: string
): Promise<RecipeDetail | null> {
  const response = await fetch(
    `${API_BASE_URL}/api/v1/recipes/${encodeURIComponent(slug)}`,
    {
      cache: "no-store",
    }
  );

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throw new Error(
      `Failed to load recipe: ${response.status}`
    );
  }

  return response.json();
}