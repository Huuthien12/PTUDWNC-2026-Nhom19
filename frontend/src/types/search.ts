import type { RecipeSummary } from "./recipe";

export interface RecipeSearchResult extends RecipeSummary {
  relevanceScore: number;
}

export interface RecipeSearchPage {
  items: RecipeSearchResult[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
