export type Difficulty = 1 | 2 | 3;
export interface Nutrition {
  calories: number | null;
  protein: number | null;
  carbs: number | null;
  fat: number | null;
}
export interface CreateRecipeRequest {
  title: string;
  description: string;
  categoryId: string;
  prepTime: number;
  cookTime: number;
  servings: number;
  difficulty: Difficulty;
  instructions?: string | null;
  nutrition?: Nutrition | null;
}
// Mutation response, independent of the Member 2 detail contract.
export interface RecipeDto extends Omit<CreateRecipeRequest, "instructions" | "nutrition"> {
  id: string;
  slug: string;
  authorId: string;
  instructions: string;
  nutrition: Nutrition;
  status: 1 | 2 | 3;
  rowVersion: string;
  createdAt: string;
  updatedAt: string;
  publishedAt: string | null;
}
