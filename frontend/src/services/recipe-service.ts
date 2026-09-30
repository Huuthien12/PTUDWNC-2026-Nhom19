import { apiClient } from "./api-client";
import { getSession } from "./auth-session";
import type { CreateRecipeRequest, RecipeDetail, RecipeDto, UpdateRecipeRequest } from "../types/recipe";

export function createRecipe(input: CreateRecipeRequest): Promise<RecipeDto> {
  return apiClient<RecipeDto>("/api/v1/recipes", {
    method: "POST", auth: true, body: JSON.stringify(input),
  });
}

export function getRecipeBySlug(slug: string): Promise<RecipeDetail> {
  return apiClient<RecipeDetail>(`/api/v1/recipes/${encodeURIComponent(slug)}`, {
    auth: getSession() !== null,
  });
}

export function updateRecipe(id: string, input: UpdateRecipeRequest): Promise<RecipeDto> {
  return apiClient<RecipeDto>(`/api/v1/recipes/${encodeURIComponent(id)}`, {
    method: "PUT", auth: true, body: JSON.stringify(input),
  });
}
