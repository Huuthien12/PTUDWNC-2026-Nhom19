import { apiClient } from "./api-client";

export interface RecipeIngredientInput {
  name: string;
  quantity: number | null;
  unit: string;
  notes: string | null;
  sortOrder: number;
  rowVersion: string;
}

export interface RecipeIngredientMutation extends Omit<RecipeIngredientInput, "rowVersion"> {
  id: string;
  rowVersion: string;
}

export function addRecipeIngredient(
  recipeId: string,
  input: RecipeIngredientInput,
): Promise<RecipeIngredientMutation> {
  return apiClient<RecipeIngredientMutation>(
    `/api/v1/recipes/${encodeURIComponent(recipeId)}/ingredients`,
    { method: "POST", auth: true, body: JSON.stringify(input) },
  );
}

export function updateRecipeIngredient(
  recipeId: string,
  ingredientId: string,
  input: RecipeIngredientInput,
): Promise<RecipeIngredientMutation> {
  return apiClient<RecipeIngredientMutation>(
    `/api/v1/recipes/${encodeURIComponent(recipeId)}/ingredients/${encodeURIComponent(ingredientId)}`,
    { method: "PUT", auth: true, body: JSON.stringify(input) },
  );
}

export async function deleteRecipeIngredient(
  recipeId: string,
  ingredientId: string,
  rowVersion: string,
): Promise<string | null> {
  let nextVersion: string | null = null;
  await apiClient<void>(
    `/api/v1/recipes/${encodeURIComponent(recipeId)}/ingredients/${encodeURIComponent(ingredientId)}`,
    {
      method: "DELETE",
      auth: true,
      body: JSON.stringify({ rowVersion }),
      onResponse: (response) => {
        const etag = response.headers.get("ETag");
        // Missing/unreadable ETag requires an aggregate reload, never reuse the old version.
        nextVersion = etag?.match(/^"([A-Za-z0-9+/]+={0,2})"$/)?.[1] ?? null;
      },
    },
  );
  return nextVersion;
}
