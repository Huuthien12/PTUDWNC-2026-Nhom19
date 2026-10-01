import { ApiError } from "./api-client";
import type { RecipeStatus } from "../types/recipe";
import type { RecipeLifecycleAction } from "./recipe-service";

export function dashboardActions(status: RecipeStatus): RecipeLifecycleAction[] {
  if (status === 1) return ["publish", "archive"];
  if (status === 2) return ["unpublish", "archive"];
  return [];
}

export function lifecycleErrorMessage(error: unknown): string {
  if (!(error instanceof ApiError)) return "Không thể cập nhật trạng thái công thức. Vui lòng thử lại.";
  if (error.status === 401) return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
  if (error.status === 403) return "Bạn không có quyền thay đổi công thức này.";
  if (error.status === 404) return "Công thức không còn khả dụng.";
  if (error.status === 400) return "Công thức chưa đủ điều kiện cho thao tác này. Vui lòng kiểm tra lại nội dung.";
  if (error.status === 409) return "Không thể thay đổi trạng thái công thức ở thời điểm này.";
  if (error.status === 422) return "Công thức đã thay đổi. Danh sách đã được tải lại.";
  return "Không thể cập nhật trạng thái công thức. Vui lòng thử lại.";
}
