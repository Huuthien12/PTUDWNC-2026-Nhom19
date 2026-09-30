export type RecipeDifficulty = 1 | 2 | 3;
export type RecipeStatus = 1 | 2 | 3;

export interface RecipeCategory {
  id: string;
  name: string;
  slug: string;
}

export interface RecipeAuthor {
  id: string;
  fullName: string;
  avatarUrl: string | null;
}

export interface RecipeNutrition {
  calories: number | null;
  protein: number | null;
  carbs: number | null;
  fat: number | null;
}

export interface RecipeIngredient {
  id: string;
  name: string;
  quantity: number | null;
  unit: string | null;
  notes: string | null;
  orderIndex: number;
}

export interface RecipeStep {
  id: string;
  stepNumber: number;
  title: string | null;
  description: string;
  timerMinutes: number | null;
  imageUrl: string | null;
}

export interface RecipeImage {
  id: string;
  originalUrl: string;
  mediumUrl: string | null;
  thumbnailUrl: string | null;
  altText: string | null;
  isPrimary: boolean;
  orderIndex: number;
}

export interface RecipeDetail {
  id: string;
  title: string;
  slug: string;
  description: string;
  instructions: string;

  prepTime: number;
  cookTime: number;
  servings: number;

  difficulty: RecipeDifficulty;
  status: RecipeStatus;
  rowVersion: string;

  createdAt: string;
  publishedAt: string | null;

  category: RecipeCategory;
  author: RecipeAuthor;

  nutrition: RecipeNutrition | null;
  ingredients: RecipeIngredient[];
  steps: RecipeStep[];
  images: RecipeImage[];
}
