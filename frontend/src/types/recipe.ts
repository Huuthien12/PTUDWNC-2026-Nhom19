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

export interface RecipePage {
  items: RecipeSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
