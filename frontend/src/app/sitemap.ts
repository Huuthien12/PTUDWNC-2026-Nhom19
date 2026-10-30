import type { MetadataRoute } from "next";
import { getCategories } from "@/services/category-service";
import { getRecipes } from "@/services/recipe-service";
import { getSiteUrl } from "@/services/recipe-seo";

const PAGE_SIZE = 50;

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const baseUrl = getSiteUrl();
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
    const urls = new Set(entries.map((entry) => entry.url));
    let page = 1;
    let totalPages = 1;
    while (page <= totalPages) {
      const recipes = await getRecipes(page, PAGE_SIZE, { sortBy: "createdAt", sortOrder: "desc" });
      const maxPages = Math.ceil(Math.max(0, recipes.totalCount) / Math.max(1, recipes.pageSize));
      totalPages = Math.min(recipes.totalPages, maxPages);
      if (recipes.page !== page) break;
      for (const recipe of recipes.items) {
        if (!recipe.publishedAt) continue;
        const url = `${baseUrl}/recipes/${encodeURIComponent(recipe.slug)}`;
        if (urls.has(url)) continue;
        urls.add(url);
        entries.push({ url, ...(recipe.publishedAt ? { lastModified: new Date(recipe.publishedAt) } : {}), changeFrequency: "weekly", priority: 0.8 });
      }
      page++;
    }
  } catch {
    // Keep static/category sitemap entries if recipe API is temporarily unavailable.
  }

  return entries;
}
