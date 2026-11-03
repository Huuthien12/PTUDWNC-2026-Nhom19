"use client";

import Image from "next/image";
import { useEffect, useRef, useState } from "react";
import { ApiError } from "@/services/api-client";
import {
  deleteRecipeImage,
  imageUploadTypes,
  setPrimaryRecipeImage,
  uploadRecipeImage,
  validateImageUpload,
} from "@/services/image-upload";
import type { RecipeDetail, RecipeImage } from "@/types/recipe";
import type { RecipeMutationLock } from "./useRecipeMutation";
import RecipeImagePreview from "../shared/RecipeImage";

interface RecipeImageManagerProps {
  recipeId: string | null;
  images: RecipeImage[];
  rowVersion: string;
  selectedFile: File | null;
  onSelectedFileChange: (file: File | null) => void;
  onImagesChange: (images: RecipeImage[]) => void;
  onRowVersionChange: (rowVersion: string) => void;
  disabled?: boolean;
  mutation?: RecipeMutationLock;
  onReload?: () => Promise<RecipeDetail>;
}

function failureMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 0) return "Không thể kết nối đến máy chủ. Kiểm tra kết nối và thử lại.";
    if (error.status === 400 || error.status === 413)
      return "Máy chủ từ chối ảnh. Kiểm tra định dạng và giới hạn 5 MiB.";
    if (error.status === 401 || error.status === 403)
      return "Bạn cần đăng nhập bằng tài khoản tác giả để thực hiện thao tác này.";
    if (error.status === 409 || error.status === 422)
      return "Công thức đã thay đổi. Tải lại dữ liệu rồi thử lại.";
    if (error.status === 503) return "Kho lưu trữ ảnh tạm thời không khả dụng. Hãy thử lại.";
    return `Không thể thay đổi ảnh (mã máy chủ: ${error.status}). Hãy thử lại.`;
  }
  return "Không thể thay đổi ảnh. Hãy thử lại.";
}

function uploadOutcomeMayBeUnknown(error: unknown): boolean {
  return !(error instanceof ApiError) ||
    error.status === 0 ||
    error.status >= 500;
}

export default function RecipeImageManager({
  recipeId,
  images,
  rowVersion,
  selectedFile,
  onSelectedFileChange,
  onImagesChange,
  onRowVersionChange,
  disabled = false,
  mutation,
  onReload,
}: RecipeImageManagerProps) {
  const [altText, setAltText] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [needsReload, setNeedsReload] = useState(false);
  const [uploadId, setUploadId] = useState<string | null>(null);
  const [preview, setPreview] = useState<{ file: File; url: string } | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);
  const fileError = selectedFile ? validateImageUpload(selectedFile) : null;
  const previewUrl = preview?.file === selectedFile ? preview.url : null;
  const canMutate = Boolean(recipeId && rowVersion) && !disabled && !busy && !mutation?.pending &&
    !mutation?.needsReload && !needsReload;

  useEffect(() => {
    if (!preview) return;
    return () => URL.revokeObjectURL(preview.url);
  }, [preview]);

  async function recoverAfterFailure(message: string) {
    mutation?.invalidate();
    if (mutation && onReload) {
      try {
        const current = await onReload();
        onImagesChange(current.images);
        onRowVersionChange(current.rowVersion);
      } catch {
        setError(`${message} Không thể tải dữ liệu mới. Hãy dùng chức năng tải lại dữ liệu trước khi tiếp tục.`);
        return;
      }
    }
    setError(message);
  }

  async function upload() {
    if (!recipeId || !selectedFile || fileError || !canMutate) return;
    if (mutation && !mutation.begin()) return;
    setBusy(true);
    setError("");
    const requestId = uploadId ?? crypto.randomUUID();
    if (!uploadId) setUploadId(requestId);
    try {
      const result = await uploadRecipeImage(recipeId, requestId, selectedFile, altText, rowVersion);
      onImagesChange([
        ...images.filter((image) => image.id !== result.image.id),
        result.image,
      ].sort((left, right) => left.orderIndex - right.orderIndex));
      onRowVersionChange(result.rowVersion);
      onSelectedFileChange(null);
      setUploadId(null);
      setPreview(null);
      setAltText("");
      if (fileInput.current) fileInput.current.value = "";
    } catch (requestError) {
      const outcomeMayBeUnknown = uploadOutcomeMayBeUnknown(requestError);
      const message = outcomeMayBeUnknown
        ? `${failureMessage(requestError)} Có thể máy chủ đã lưu ảnh nhưng phản hồi không được nhận. Có thể thử lại an toàn với cùng ảnh.`
        : failureMessage(requestError);
      await recoverAfterFailure(message);
    } finally {
      mutation?.end();
      setBusy(false);
    }
  }

  async function makePrimary(imageId: string) {
    if (!recipeId || !canMutate || mutation && !mutation.begin()) return;
    setBusy(true);
    setError("");
    try {
      const result = await setPrimaryRecipeImage(recipeId, imageId, rowVersion);
      onImagesChange(images.map((image) => ({ ...image, isPrimary: image.id === result.image.id })));
      onRowVersionChange(result.rowVersion);
    } catch (requestError) {
      await recoverAfterFailure(failureMessage(requestError));
    } finally {
      mutation?.end();
      setBusy(false);
    }
  }

  async function remove(image: RecipeImage) {
    if (!recipeId || !canMutate || mutation && !mutation.begin()) return;
    if (!window.confirm("Bạn có chắc muốn xóa ảnh này khỏi công thức?")) {
      mutation?.end();
      return;
    }
    setBusy(true);
    setError("");
    try {
      const nextRowVersion = await deleteRecipeImage(recipeId, image.id, rowVersion);
      const remainingImages = images.filter((item) => item.id !== image.id);
      if (image.isPrimary && remainingImages.length) {
        const nextPrimaryId = [...remainingImages].sort((left, right) => left.orderIndex - right.orderIndex)[0].id;
        onImagesChange(remainingImages.map((item) => ({ ...item, isPrimary: item.id === nextPrimaryId })));
      } else {
        onImagesChange(remainingImages);
      }
      if (nextRowVersion) onRowVersionChange(nextRowVersion);
      if (onReload) {
        try {
          const current = await onReload();
          onImagesChange(current.images);
          onRowVersionChange(current.rowVersion);
          setNeedsReload(false);
        } catch {
          setNeedsReload(true);
          mutation?.invalidate();
          setError("Ảnh đã bị xóa, nhưng không thể tải lại trạng thái mới. Hãy tải lại dữ liệu trước khi tiếp tục.");
        }
      } else if (!nextRowVersion) {
        setNeedsReload(true);
        setError("Ảnh đã bị xóa nhưng không nhận được RowVersion mới. Hãy tải lại dữ liệu trước khi thực hiện thay đổi tiếp theo.");
      }
    } catch (requestError) {
      await recoverAfterFailure(failureMessage(requestError));
    } finally {
      mutation?.end();
      setBusy(false);
    }
  }

  return (
    <section aria-labelledby="recipe-images-heading" className="space-y-4 rounded-lg border p-4">
      <div>
        <h2 id="recipe-images-heading" className="text-lg font-semibold">Hình ảnh</h2>
        <p className="text-sm text-gray-600">JPEG, PNG, WebP hoặc AVIF, tối đa 5 MiB.</p>
      </div>

      {images.length > 0 ? (
        <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          {[...images].sort((left, right) => left.orderIndex - right.orderIndex).map((image) => (
            <li key={image.id} className="space-y-2 rounded-lg border p-3">
              <RecipeImagePreview
                src={image.originalUrl}
                mediumSrc={image.mediumUrl}
                thumbnailSrc={image.thumbnailUrl}
                title={image.altText || "Ảnh công thức"}
                alt={image.altText || "Ảnh công thức"}
              />
              <p className="text-sm text-gray-700">
                {image.isPrimary ? "Ảnh đại diện" : "Ảnh bổ sung"}
                {image.altText ? ` · ${image.altText}` : ""}
              </p>
              <div className="flex flex-wrap gap-2">
                {!image.isPrimary && (
                  <button type="button" disabled={!canMutate} onClick={() => void makePrimary(image.id)}
                    className="rounded border px-3 py-2 text-sm disabled:opacity-50">
                    Đặt làm ảnh đại diện
                  </button>
                )}
                <button type="button" disabled={!canMutate} onClick={() => void remove(image)}
                  className="rounded border px-3 py-2 text-sm text-red-700 disabled:opacity-50">
                  Xóa ảnh
                </button>
              </div>
            </li>
          ))}
        </ul>
      ) : (
        <RecipeImagePreview title="Chưa có ảnh công thức" alt="Chưa có ảnh công thức" />
      )}

      {selectedFile && (
        <div className="space-y-2">
          <h3 className="font-medium">Xem trước ảnh đã chọn</h3>
          {fileError ? (
            <p role="alert" className="text-sm text-red-700">{fileError}</p>
          ) : previewUrl && (
            <div className="cb-recipe-visual">
              <Image src={previewUrl} alt={altText || selectedFile.name}
                fill sizes="(max-width: 640px) 100vw, 50vw" unoptimized />
            </div>
          )}
        </div>
      )}

      <div className="space-y-3">
        <label htmlFor="recipe-image-file" className="block font-medium">Chọn ảnh</label>
        <input ref={fileInput} id="recipe-image-file" type="file" accept={imageUploadTypes.join(",")}
          disabled={disabled || busy || mutation?.pending} onChange={(event) => {
            const file = event.currentTarget.files?.[0] ?? null;
            setPreview(file ? { file, url: URL.createObjectURL(file) } : null);
            setUploadId(file ? crypto.randomUUID() : null);
            onSelectedFileChange(file);
            setError("");
          }} className="block w-full text-sm file:mr-3 file:rounded file:border-0 file:bg-orange-100 file:px-3 file:py-2" />
        <label htmlFor="recipe-image-alt" className="block text-sm font-medium">Văn bản thay thế (không bắt buộc)</label>
        <input id="recipe-image-alt" value={altText} maxLength={200} disabled={disabled || busy || mutation?.pending}
          onChange={(event) => setAltText(event.target.value)} className="w-full rounded border p-2" />
        {!recipeId && <p role="status" className="text-sm text-gray-600">Lưu công thức để bật tính năng tải ảnh lên.</p>}
        {selectedFile && !fileError && (
          <button type="button" disabled={!canMutate} onClick={() => void upload()}
            className="rounded-lg bg-orange-600 px-4 py-2 font-semibold text-white disabled:opacity-50">
            {busy ? "Đang tải ảnh..." : error ? "Thử tải lại ảnh" : "Tải ảnh lên"}
          </button>
        )}
        {error && <p role="alert" className="text-sm text-red-700">{error}</p>}
      </div>
    </section>
  );
}
