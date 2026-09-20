import { apiClient } from "@/services/api-client";
import type {
  Category,
  CategoryDetail,
} from "@/types/category";

export interface CategoryInput {
  name: string;
  description: string | null;
}

export async function getCategories(): Promise<Category[]> {
  return apiClient<Category[]>("/api/v1/categories", {
    cache: "no-store",
  });
}

export async function getCategoryBySlug(
  slug: string,
  page = 1,
  pageSize = 12
): Promise<CategoryDetail> {
  const query = new URLSearchParams({
    page: page.toString(),
    pageSize: pageSize.toString(),
  });

  return apiClient<CategoryDetail>(
    `/api/v1/categories/${encodeURIComponent(slug)}?${query.toString()}`,
    {
      cache: "no-store",
    }
  );
}

export async function createCategory(
  input: CategoryInput
): Promise<Category> {
  return apiClient<Category>("/api/v1/categories", {
    method: "POST",
    credentials: "include",
    body: JSON.stringify(input),
  });
}

export async function updateCategory(
  id: string,
  input: CategoryInput
): Promise<Category> {
  return apiClient<Category>(
    `/api/v1/categories/${id}`,
    {
      method: "PUT",
      credentials: "include",
      body: JSON.stringify(input),
    }
  );
}

export async function deleteCategory(
  id: string
): Promise<void> {
  return apiClient<void>(
    `/api/v1/categories/${id}`,
    {
      method: "DELETE",
      credentials: "include",
    }
  );
}