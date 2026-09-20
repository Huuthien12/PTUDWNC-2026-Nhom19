export interface Category {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  recipeCount: number;
}

export interface RecipeSummary {
  id: string;
  title: string;
  slug: string;
  description: string;
  prepTime: number;
  cookTime: number;
  servings: number;
  difficulty: number | string;
  thumbnailUrl: string | null;
  publishedAt: string | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface CategoryDetail {
  category: Category;
  recipes: PagedResult<RecipeSummary>;
}