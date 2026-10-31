import { apiClient } from "./api-client";
import type { RecipeImage } from "../types/recipe";

export const imageUploadTypes = ["image/jpeg", "image/png", "image/webp", "image/avif"] as const;
export const maxImageUploadBytes = 5 * 1024 * 1024;
export type ImageUploadState = "idle" | "uploading" | "success" | "error";

export function validateImageUpload(file: Pick<File, "size" | "type"> | null): string | null {
  if (!file) return "Vui lòng chọn ảnh để tải lên.";
  if (file.size === 0) return "Ảnh không được để trống.";
  if (file.size > maxImageUploadBytes) return "Ảnh không được vượt quá 5 MiB.";
  if (!imageUploadTypes.includes(file.type as typeof imageUploadTypes[number]))
    return "Chỉ chấp nhận ảnh JPEG, PNG, WebP hoặc AVIF.";
  return null;
}

interface RecipeImageMutationResponse extends Omit<RecipeImage, "id"> {
  imageId: string;
  url: string;
  rowVersion: string;
}

export interface RecipeImageMutationResult {
  image: RecipeImage;
  rowVersion: string;
}

function mapImageResponse(response: RecipeImageMutationResponse): RecipeImageMutationResult {
  return {
    image: {
      id: response.imageId,
      originalUrl: response.originalUrl,
      mediumUrl: response.mediumUrl,
      thumbnailUrl: response.thumbnailUrl,
      altText: response.altText,
      isPrimary: response.isPrimary,
      orderIndex: response.orderIndex,
    },
    rowVersion: response.rowVersion,
  };
}

function recipeImagePath(recipeId: string): string {
  return `/api/v1/recipes/${encodeURIComponent(recipeId)}/images`;
}

export async function uploadRecipeImage(
  recipeId: string,
  uploadId: string,
  file: File,
  altText: string,
  rowVersion: string,
): Promise<RecipeImageMutationResult> {
  const form = new FormData();
  form.append("uploadId", uploadId);
  form.append("file", file);
  form.append("rowVersion", rowVersion);
  if (altText.trim()) form.append("altText", altText.trim());
  const response = await apiClient<RecipeImageMutationResponse>(recipeImagePath(recipeId), {
    method: "POST",
    auth: true,
    body: form,
  });
  return mapImageResponse(response);
}

export async function setPrimaryRecipeImage(
  recipeId: string,
  imageId: string,
  rowVersion: string,
): Promise<RecipeImageMutationResult> {
  const response = await apiClient<RecipeImageMutationResponse>(
    `${recipeImagePath(recipeId)}/${encodeURIComponent(imageId)}/primary`,
    { method: "PATCH", auth: true, body: JSON.stringify({ rowVersion }) },
  );
  return mapImageResponse(response);
}

export async function deleteRecipeImage(
  recipeId: string,
  imageId: string,
  rowVersion: string,
): Promise<string | null> {
  let nextRowVersion: string | null = null;
  await apiClient<void>(`${recipeImagePath(recipeId)}/${encodeURIComponent(imageId)}`, {
    method: "DELETE",
    auth: true,
    body: JSON.stringify({ rowVersion }),
    onResponse(response) {
      const etag = response.headers.get("ETag");
      nextRowVersion = etag?.replace(/^W\//i, "").replace(/^"(.*)"$/, "$1") ?? null;
    },
  });
  return nextRowVersion;
}
