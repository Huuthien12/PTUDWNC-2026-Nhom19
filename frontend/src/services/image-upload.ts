export const imageUploadTypes = ["image/jpeg", "image/png", "image/webp", "image/avif"] as const;
export const maxImageUploadBytes = 5 * 1024 * 1024;
export type ImageUploadState = "idle" | "uploading" | "success" | "error";

export function validateImageUpload(file: Pick<File, "size" | "type"> | null): string | null {
  if (!file) return "Vui lòng chọn ảnh để tải lên.";
  if (file.size > maxImageUploadBytes) return "Ảnh không được vượt quá 5 MiB.";
  if (!imageUploadTypes.includes(file.type as typeof imageUploadTypes[number]))
    return "Chỉ chấp nhận ảnh JPEG, PNG, WebP hoặc AVIF.";
  return null;
}
