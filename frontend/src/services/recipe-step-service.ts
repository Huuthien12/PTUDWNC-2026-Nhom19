import { apiClient } from "./api-client";
import type { RecipeStep } from "../types/recipe";

interface RecipeStepApiItem {
  id: string;
  stepNumber: number;
  title: string | null;
  description: string;
  timerMinutes: number | null;
  imageUrl: string | null;
  rowVersion: string;
}

interface RecipeStepApiResult {
  items: RecipeStepApiItem[];
  rowVersion: string;
}

export interface RecipeStepMutationResult {
  items: RecipeStep[];
  rowVersion: string;
}

export interface CreateRecipeStepRequest {
  title?: string | null;
  description: string;
  timerMinutes?: number | null;
  rowVersion: string;
}

export interface UpdateRecipeStepRequest {
  stepNumber: number;
  title?: string | null;
  description: string;
  timerMinutes?: number | null;
  rowVersion: string;
}

function normalizeResult(
  result: RecipeStepApiResult,
): RecipeStepMutationResult {
  return {
    items: result.items.map((step) => ({
      id: step.id,
      stepNumber: step.stepNumber,
      title: step.title,
      description: step.description,
      timerMinutes: step.timerMinutes,
      imageUrl: step.imageUrl,
      rowVersion: step.rowVersion,
    })),
    rowVersion: result.rowVersion,
  };
}

export async function createRecipeStep(
  recipeId: string,
  input: CreateRecipeStepRequest,
): Promise<RecipeStepMutationResult> {
  const result = await apiClient<RecipeStepApiResult>(
    `/api/v1/recipes/${encodeURIComponent(recipeId)}/steps`,
    {
      method: "POST",
      auth: true,
      body: JSON.stringify({
        title: input.title ?? null,
        description: input.description,
        durationMinutes: input.timerMinutes ?? null,
        imageUrl: null,
        rowVersion: input.rowVersion,
      }),
    },
  );

  return normalizeResult(result);
}

export async function updateRecipeStep(
  recipeId: string,
  stepId: string,
  input: UpdateRecipeStepRequest,
): Promise<RecipeStepMutationResult> {
  const result = await apiClient<RecipeStepApiResult>(
    `/api/v1/recipes/${encodeURIComponent(recipeId)}/steps/${encodeURIComponent(stepId)}`,
    {
      method: "PUT",
      auth: true,
      body: JSON.stringify({
        title: input.title ?? null,
        description: input.description,
        durationMinutes: input.timerMinutes ?? null,
        imageUrl: null,
        rowVersion: input.rowVersion,
      }),
    },
  );

  return normalizeResult(result);
}

export async function deleteRecipeStep(
  recipeId: string,
  stepId: string,
  rowVersion: string,
): Promise<string | null> {
  let nextVersion: string | null = null;
  await apiClient<void>(
    `/api/v1/recipes/${encodeURIComponent(recipeId)}/steps/${encodeURIComponent(stepId)}`,
    {
      method: "DELETE",
      auth: true,
      body: JSON.stringify({ rowVersion }),
      onResponse: (response) => {
        const etag = response.headers.get("ETag");
        nextVersion = etag?.match(/^"([A-Za-z0-9+/]+={0,2})"$/)?.[1] ?? null;
      },
    },
  );
  return nextVersion;
}
