import type { RecipeSummary } from "./recipe";
import type { PaginatedResult } from "./api";

export interface RecipeSearchResult extends RecipeSummary {
  relevanceScore: number;
}

export interface RecipeSearchPage extends PaginatedResult<RecipeSearchResult> {}
