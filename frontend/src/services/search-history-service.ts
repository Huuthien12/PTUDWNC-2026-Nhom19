import { apiClient } from "./api-client";
export type SearchHistoryItem = { id: string; query: string; searchedAt: string };
export const getSearchHistory = () => apiClient<SearchHistoryItem[]>("/api/v1/search-history", { auth: true, cache: "no-store" });
export const saveSearchHistory = (query: string) => apiClient<SearchHistoryItem>("/api/v1/search-history", { method: "POST", auth: true, body: JSON.stringify({ query }) });
export const deleteSearchHistory = (id: string) => apiClient<void>(`/api/v1/search-history/${encodeURIComponent(id)}`, { method: "DELETE", auth: true });
export const clearSearchHistory = () => apiClient<void>("/api/v1/search-history", { method: "DELETE", auth: true });
