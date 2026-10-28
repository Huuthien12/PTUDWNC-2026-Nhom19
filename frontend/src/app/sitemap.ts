import type { MetadataRoute } from "next";
import { getCategories } from "@/services/category-service";
import { getRecipes } from "@/services/recipe-service";

const DEFAULT_SITE_URL = "http://localhost:3000";

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const baseUrl = (process.env.NEXT_PUBLIC_SITE_URL ?? DEFAULT_SITE_URL).replace(/\/+$/, "");
  const entries: MetadataRoute.Sitemap = [
    { url: `${baseUrl}/`, changeFrequency: "daily", priority: 1 },
    { url: `${baseUrl}/recipes`, changeFrequency: "daily", priority: 0.9 },
    { url: `${baseUrl}/categories`, changeFrequency: "weekly", priority: 0.8 },
  ];

  try {
    const categories = await getCategories();
    for (const category of categories) {
      entries.push({
        url: `${baseUrl}/categories/${encodeURIComponent(category.slug)}`,
        changeFrequency: "weekly",
        priority: 0.7,
      });
    }
  } catch {
    // Keep static sitemap entries if category API is temporarily unavailable.
  }

  try {
    const recipes = await getRecipes(1, 50, { sortBy: "createdAt", sortOrder: "desc" });
    for (const recipe of recipes.items) {
      entries.push({
        url: `${baseUrl}/recipes/${encodeURIComponent(recipe.slug)}`,
        ...(recipe.publishedAt ? { lastModified: new Date(recipe.publishedAt) } : {}),
        changeFrequency: "weekly",
        priority: 0.8,
      });
    }
  } catch {
    // Keep static/category sitemap entries if recipe API is temporarily unavailable.
  }

  return entries;
}
