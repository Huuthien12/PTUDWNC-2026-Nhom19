import { apiClient } from "./api-client";
import type { CreateRecipeRequest, RecipeDto } from "../types/recipe";

export function createRecipe(input: CreateRecipeRequest): Promise<RecipeDto> {
  return apiClient<RecipeDto>("/api/v1/recipes", {
    method: "POST", auth: true, body: JSON.stringify(input),
  });
}
