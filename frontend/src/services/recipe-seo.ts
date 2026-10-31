import type { Metadata } from "next";
import type { Category } from "@/types/category";
import type { RecipeDetail } from "@/types/recipe";

const DEFAULT_SITE_URL = "http://localhost:3000";

export function getSiteUrl(siteUrl = process.env.NEXT_PUBLIC_SITE_URL): string {
  return (siteUrl || DEFAULT_SITE_URL).replace(/\/+$/, "");
}

export function serializeJsonLd(value: Record<string, unknown>): string {
  return JSON.stringify(value).replace(/</g, "\\u003c");
}

export function buildSiteMetadata(siteUrl = getSiteUrl()): Metadata {
  const baseUrl = getSiteUrl(siteUrl);
  return {
    metadataBase: new URL(baseUrl),
    title: "Culinary Blog",
    description: "Chia sẻ và khám phá các công thức nấu ăn",
    alternates: { canonical: "/" },
    openGraph: {
      title: "Culinary Blog",
      description: "Chia sẻ và khám phá các công thức nấu ăn",
      url: "/",
      siteName: "Culinary Blog",
      locale: "vi_VN",
      type: "website",
    },
  };
}

export function buildRecipeMetadata(recipe: RecipeDetail, siteUrl = getSiteUrl()): Metadata {
  const baseUrl = getSiteUrl(siteUrl);
  const canonicalUrl = `${baseUrl}/recipes/${encodeURIComponent(recipe.slug)}`;
  const primaryImage = recipe.images.find((item) => item.isPrimary) ?? recipe.images[0];
  const imageUrl = primaryImage?.mediumUrl ?? primaryImage?.originalUrl ?? undefined;
  const description = recipe.description?.trim() || `Công thức nấu món ${recipe.title} trên Culinary Blog.`;

  return {
    title: `${recipe.title} | Culinary Blog`,
    description,
    alternates: { canonical: canonicalUrl },
    openGraph: {
      title: recipe.title,
      description,
      url: canonicalUrl,
      siteName: "Culinary Blog",
      locale: "vi_VN",
      type: "article",
      ...(imageUrl ? { images: [{ url: imageUrl, alt: primaryImage?.altText ?? recipe.title }] } : {}),
    },
  };
}

export function buildCategoryMetadata(
  category: Pick<Category, "name" | "slug" | "description">,
  siteUrl = getSiteUrl()
): Metadata {
  const baseUrl = getSiteUrl(siteUrl);
  const canonicalUrl = `${baseUrl}/categories/${encodeURIComponent(category.slug)}`;
  const description =
    category.description?.trim() ||
    `Khám phá các công thức thuộc danh mục ${category.name} trên Culinary Blog.`;

  return {
    title: `${category.name} | Culinary Blog`,
    description,
    alternates: { canonical: canonicalUrl },
    openGraph: {
      title: `${category.name} | Culinary Blog`,
      description,
      url: canonicalUrl,
      siteName: "Culinary Blog",
      locale: "vi_VN",
      type: "website",
    },
  };
}

export function buildRecipeJsonLd(recipe: RecipeDetail, siteUrl = getSiteUrl()): Record<string, unknown> {
  const baseUrl = getSiteUrl(siteUrl);
  const primaryImage = recipe.images.find((item) => item.isPrimary) ?? recipe.images[0];
  const imageUrl = primaryImage?.originalUrl ?? primaryImage?.mediumUrl ?? null;

  return {
    "@context": "https://schema.org",
    "@type": "Recipe",
    name: recipe.title,
    description: recipe.description,
    url: `${baseUrl}/recipes/${encodeURIComponent(recipe.slug)}`,
    author: {
      "@type": "Person",
      name: recipe.author.fullName,
    },
    recipeCategory: recipe.category.name,
    prepTime: `PT${Math.max(0, recipe.prepTime)}M`,
    cookTime: `PT${Math.max(0, recipe.cookTime)}M`,
    totalTime: `PT${Math.max(0, recipe.prepTime + recipe.cookTime)}M`,
    recipeYield: `${recipe.servings} khẩu phần`,
    recipeIngredient: recipe.ingredients.map((item) =>
      [item.quantity, item.unit, item.name].filter((part) => part !== null && part !== "").join(" ").trim()
    ),
    recipeInstructions: recipe.steps.map((step) => ({
      "@type": "HowToStep",
      position: step.stepNumber,
      name: step.title ?? `Bước ${step.stepNumber}`,
      text: step.description,
    })),
    ...(imageUrl ? { image: [imageUrl] } : {}),
    ...(recipe.nutrition?.calories != null
      ? {
          nutrition: {
            "@type": "NutritionInformation",
            calories: `${recipe.nutrition.calories} calories`,
          },
        }
      : {}),
  };
}
