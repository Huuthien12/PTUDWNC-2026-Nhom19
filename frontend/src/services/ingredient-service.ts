import { apiClient } from "./api-client";
import type { RecipeIngredient } from "../types/recipe";

export interface RecipeIngredientInput {
  name: string;
  quantity: number | null;
  unit: string;
  notes: string | null;
  sortOrder: number;
  rowVersion: string;
}

export function addRecipeIngredient(
  recipeId: string,
  input: RecipeIngredientInput,
): Promise<RecipeIngredient & { rowVersion: string }> {
  return apiClient<RecipeIngredient & { rowVersion: string }>(
    `/api/v1/recipes/${encodeURIComponent(recipeId)}/ingredients`,
    { method: "POST", auth: true, body: JSON.stringify(input) },
  );
}

export function updateRecipeIngredient(
  recipeId: string,
  ingredientId: string,
  input: RecipeIngredientInput,
): Promise<RecipeIngredient & { rowVersion: string }> {
  return apiClient<RecipeIngredient & { rowVersion: string }>(
    `/api/v1/recipes/${encodeURIComponent(recipeId)}/ingredients/${encodeURIComponent(ingredientId)}`,
    { method: "PUT", auth: true, body: JSON.stringify(input) },
  );
}

export function deleteRecipeIngredient(
  recipeId: string,
  ingredientId: string,
  rowVersion: string,
): Promise<void> {
  return apiClient<void>(
    `/api/v1/recipes/${encodeURIComponent(recipeId)}/ingredients/${encodeURIComponent(ingredientId)}`,
    {
      method: "DELETE",
      auth: true,
      body: JSON.stringify({ rowVersion }),
    },
  );
}
