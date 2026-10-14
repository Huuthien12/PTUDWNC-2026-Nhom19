import type { PaginatedResult } from "./api";
import type { RecipeSummary } from "./recipe";

export type { RecipeSummary } from "./recipe";

export interface Category {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  recipeCount: number;
}

export type PagedResult<T> = PaginatedResult<T>;

export interface CategoryDetail {
  category: Category;
  recipes: PaginatedResult<RecipeSummary>;
}
