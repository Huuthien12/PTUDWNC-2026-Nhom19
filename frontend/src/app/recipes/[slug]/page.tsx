import type { Metadata } from "next";
import RecipeDetailClient from "@/components/recipes/RecipeDetailClient";
import { getRecipeBySlug } from "@/services/recipe-service";
import { buildRecipeJsonLd, buildRecipeMetadata, getSiteUrl, serializeJsonLd } from "@/services/recipe-seo";
import type { RecipeDetail } from "@/types/recipe";

type PageProps = { params: Promise<{ slug: string }> };

async function getPublishedRecipe(slug: string): Promise<RecipeDetail | null> {
  try {
    const recipe = await getRecipeBySlug(slug);
    return recipe.status === 2 ? recipe : null;
  } catch {
    return null;
  }
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const recipe = await getPublishedRecipe((await params).slug);
  return recipe ? buildRecipeMetadata(recipe, getSiteUrl()) : { robots: { index: false, follow: false } };
}

export default async function RecipeDetailPage({ params }: PageProps) {
  const slug = (await params).slug;
  const recipe = await getPublishedRecipe(slug);
  return <>
    {recipe && <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: serializeJsonLd(buildRecipeJsonLd(recipe, getSiteUrl())) }} />}
    <RecipeDetailClient slug={slug} />
  </>;
}
