import assert from "node:assert/strict";
import { afterEach, beforeEach, test } from "node:test";
import { useState } from "react";
import { act, create, type ReactTestInstance, type ReactTestRenderer } from "react-test-renderer";
import RecipeImageManager from "../src/components/recipes/RecipeImageManager";
import { SESSION_KEY } from "../src/services/auth-session";
import type { RecipeImage } from "../src/types/recipe";

Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
const originalFetch = globalThis.fetch;
const originalWindow = Object.getOwnPropertyDescriptor(globalThis, "window");
const originalSelf = Object.getOwnPropertyDescriptor(globalThis, "self");
const originalCreateObjectURL = Object.getOwnPropertyDescriptor(URL, "createObjectURL");
const originalRevokeObjectURL = Object.getOwnPropertyDescriptor(URL, "revokeObjectURL");
let view: ReactTestRenderer | undefined;
let calls: { url: string; method: string; body: BodyInit | null }[];
let uploadFailureCount: number;
let uploadFailureStatus: number;
let uploadCommitBeforeFailure: boolean;
let uploadTransportFailureCount: number;
let serverUploads: Map<string, typeof imageResponse>;
const rowVersion = Buffer.from("recipe-v1").toString("base64");
const imageResponse = {
  imageId: "image-1",
  url: "https://storage.example/recipes/image.jpg",
  originalUrl: "https://storage.example/recipes/image.jpg",
  mediumUrl: "https://storage.example/recipes/image-medium.jpg",
  thumbnailUrl: "https://storage.example/recipes/image-thumbnail.jpg",
  altText: "A bowl of soup",
  isPrimary: true,
  orderIndex: 0,
  rowVersion: Buffer.from("recipe-v2").toString("base64"),
};

function ManagerHost({ initialImages = [] }: { initialImages?: RecipeImage[] }) {
  const [images, setImages] = useState<RecipeImage[]>(initialImages);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [currentVersion, setCurrentVersion] = useState(rowVersion);
  return <RecipeImageManager recipeId="recipe-1" images={images} rowVersion={currentVersion}
    selectedFile={selectedFile} onSelectedFileChange={setSelectedFile} onImagesChange={setImages}
    onRowVersionChange={setCurrentVersion} />;
}

beforeEach(() => {
  Object.defineProperty(globalThis, "self", { configurable: true, value: globalThis });
  const session = {
    sessionId: "test",
    accessToken: "test-access",
    refreshToken: "test-refresh",
    tokenType: "Bearer",
    expiresAt: new Date(Date.now() + 600000).toISOString(),
    user: { id: "owner", fullName: "Owner", email: "owner@example.test", userName: "owner",
      avatarUrl: null, roles: ["Author"] },
  };
  Object.defineProperty(globalThis, "window", {
    configurable: true,
    value: Object.assign(new EventTarget(), {
      confirm: () => true,
      localStorage: { getItem: (key: string) => key === SESSION_KEY ? JSON.stringify(session) : null },
    }),
  });
  Object.defineProperty(URL, "createObjectURL", { configurable: true, value: () => "blob:recipe-preview" });
  Object.defineProperty(URL, "revokeObjectURL", { configurable: true, value: () => {} });
  calls = [];
  uploadFailureCount = 0;
  uploadFailureStatus = 503;
  uploadCommitBeforeFailure = false;
  uploadTransportFailureCount = 0;
  serverUploads = new Map();
  globalThis.fetch = async (input, options = {}) => {
    const url = new URL(String(input), "http://localhost");
    const method = options.method ?? "GET";
    calls.push({ url: `${url.pathname}${url.search}`, method, body: options.body ?? null });
    assert.ok(new Headers(options.headers).get("Authorization"));
    if (method === "POST") {
      const uploadForm = options.body as FormData;
      const uploadId = String(uploadForm.get("uploadId"));
      const savedResponse = serverUploads.get(uploadId);
      if (savedResponse) return Response.json(savedResponse, { status: 201 });
      if (uploadFailureCount > 0) {
        uploadFailureCount--;
        if (uploadCommitBeforeFailure) serverUploads.set(uploadId, imageResponse);
        return Response.json({ type: "upload-failed", status: uploadFailureStatus }, { status: uploadFailureStatus });
      }
      if (uploadTransportFailureCount > 0) {
        uploadTransportFailureCount--;
        if (uploadCommitBeforeFailure) serverUploads.set(uploadId, imageResponse);
        throw new TypeError("network timeout");
      }
      serverUploads.set(uploadId, imageResponse);
      return Response.json(imageResponse, { status: 201 });
    }
    if (method === "DELETE")
      return new Response(null, { status: 204, headers: { ETag: `"${Buffer.from("recipe-v3").toString("base64")}"` } });
    return Response.json(imageResponse, { status: method === "POST" ? 201 : 200 });
  };
});

afterEach(async () => {
  if (view) await act(async () => { view!.unmount(); });
  view = undefined;
  globalThis.fetch = originalFetch;
  if (originalWindow) Object.defineProperty(globalThis, "window", originalWindow);
  else Reflect.deleteProperty(globalThis, "window");
  if (originalSelf) Object.defineProperty(globalThis, "self", originalSelf);
  else Reflect.deleteProperty(globalThis, "self");
  if (originalCreateObjectURL) Object.defineProperty(URL, "createObjectURL", originalCreateObjectURL);
  else Reflect.deleteProperty(URL, "createObjectURL");
  if (originalRevokeObjectURL) Object.defineProperty(URL, "revokeObjectURL", originalRevokeObjectURL);
  else Reflect.deleteProperty(URL, "revokeObjectURL");
});

async function dispatch(action: () => void) {
  await act(async () => {
    action();
    await new Promise<void>((resolve) => setImmediate(resolve));
  });
}

function text(node: ReactTestInstance): string {
  return node.children.map((child) => typeof child === "string" ? child : text(child)).join("");
}

function button(label: string) {
  return view!.root.findAllByType("button").find((item) => text(item) === label)!;
}

test("selecting a valid image previews it and uploads through the shared multipart API", async () => {
  const file = new File(["image bytes"], "soup.png", { type: "image/png" });
  await dispatch(() => { view = create(<ManagerHost />); });
  const input = view!.root.findByProps({ id: "recipe-image-file" });
  await dispatch(() => input.props.onChange({ currentTarget: { files: [file] } }));

  assert.equal(view!.root.findByType("img").props.src, "blob:recipe-preview");
  assert.ok(button("Tải ảnh lên"));
  await dispatch(() => button("Tải ảnh lên").props.onClick());

  const upload = calls.at(0);
  assert.ok(upload);
  assert.equal(upload.url, "/api/recipe-cache/api/v1/recipes/recipe-1/images");
  assert.equal(upload.method, "POST");
  assert.ok(upload.body instanceof FormData);
  assert.equal((upload.body as FormData).get("rowVersion"), rowVersion);
  assert.ok((upload.body as FormData).get("uploadId"));
  const uploadedFile = (upload.body as FormData).get("file");
  assert.ok(uploadedFile instanceof File);
  assert.equal(uploadedFile.name, "soup.png");
  assert.equal((upload.body as FormData).get("altText"), null);
  assert.match(text(view!.root), /Ảnh đại diện/);
  assert.equal(view!.root.findAllByType("img").length, 1);
});

test("replaying a committed upload updates the image instead of displaying it twice", async () => {
  const file = new File(["image bytes"], "soup.png", { type: "image/png" });
  const existingImage: RecipeImage = {
    id: imageResponse.imageId,
    originalUrl: imageResponse.originalUrl,
    mediumUrl: imageResponse.mediumUrl,
    thumbnailUrl: imageResponse.thumbnailUrl,
    altText: imageResponse.altText,
    isPrimary: imageResponse.isPrimary,
    orderIndex: imageResponse.orderIndex,
  };
  await dispatch(() => { view = create(<ManagerHost initialImages={[existingImage]} />); });
  const input = view!.root.findByProps({ id: "recipe-image-file" });
  await dispatch(() => input.props.onChange({ currentTarget: { files: [file] } }));
  await dispatch(() => button("Tải ảnh lên").props.onClick());

  assert.equal(view!.root.findAllByType("li").length, 1);
});

test("upload errors preserve the selected image and allow retry", async () => {
  const file = new File(["image bytes"], "soup.png", { type: "image/png" });
  uploadFailureCount = 1;
  await dispatch(() => { view = create(<ManagerHost />); });
  const input = view!.root.findByProps({ id: "recipe-image-file" });
  await dispatch(() => input.props.onChange({ currentTarget: { files: [file] } }));
  await dispatch(() => button("Tải ảnh lên").props.onClick());

  assert.match(text(view!.root), /Kho lưu trữ ảnh tạm thời không khả dụng/);
  assert.equal(view!.root.findByType("img").props.src, "blob:recipe-preview");
  await dispatch(() => button("Thử tải lại ảnh").props.onClick());

  assert.equal(calls.length, 2);
  assert.equal(
    (calls[0].body as FormData).get("uploadId"),
    (calls[1].body as FormData).get("uploadId"),
  );
  assert.match(text(view!.root), /Ảnh đại diện/);
  assert.equal(view!.root.findByType("img").props.src, imageResponse.originalUrl);
});

test("503 after server commit can be retried with the same upload ID without duplicating the image", async () => {
  const file = new File(["image bytes"], "soup.png", { type: "image/png" });
  uploadFailureCount = 1;
  uploadCommitBeforeFailure = true;
  await dispatch(() => { view = create(<ManagerHost />); });
  const input = view!.root.findByProps({ id: "recipe-image-file" });
  await dispatch(() => input.props.onChange({ currentTarget: { files: [file] } }));
  await dispatch(() => button("Tải ảnh lên").props.onClick());

  assert.match(text(view!.root), /Có thể máy chủ đã lưu ảnh/);
  assert.equal(view!.root.findByType("img").props.src, "blob:recipe-preview");
  assert.equal(button("Thử tải lại ảnh").props.disabled, false);

  await dispatch(() => button("Thử tải lại ảnh").props.onClick());
  const uploads = calls.filter((call) => call.method === "POST");
  assert.equal(uploads.length, 2);
  assert.equal(
    (uploads[0].body as FormData).get("uploadId"),
    (uploads[1].body as FormData).get("uploadId"),
  );
  assert.equal(serverUploads.size, 1);
  assert.match(text(view!.root), /Ảnh đại diện/);
});

test("timeout after server commit retries with the same upload ID", async () => {
  const file = new File(["image bytes"], "soup.png", { type: "image/png" });
  uploadTransportFailureCount = 1;
  uploadCommitBeforeFailure = true;
  await dispatch(() => { view = create(<ManagerHost />); });
  const input = view!.root.findByProps({ id: "recipe-image-file" });
  await dispatch(() => input.props.onChange({ currentTarget: { files: [file] } }));
  await dispatch(() => button("Tải ảnh lên").props.onClick());

  assert.match(text(view!.root), /Có thể máy chủ đã lưu ảnh/);
  await dispatch(() => button("Thử tải lại ảnh").props.onClick());

  const uploads = calls.filter((call) => call.method === "POST");
  assert.equal(uploads.length, 2);
  assert.equal(
    (uploads[0].body as FormData).get("uploadId"),
    (uploads[1].body as FormData).get("uploadId"),
  );
  assert.equal(serverUploads.size, 1);
  assert.match(text(view!.root), /Ảnh đại diện/);
});

test("frontend upload validation blocks empty, oversized, and unsupported files", async () => {
  await dispatch(() => { view = create(<ManagerHost />); });
  const input = view!.root.findByProps({ id: "recipe-image-file" });
  const invalidFiles = [
    new File([], "empty.png", { type: "image/png" }),
    new File([new Uint8Array(5 * 1024 * 1024 + 1)], "large.png", { type: "image/png" }),
    new File(["text"], "notes.txt", { type: "text/plain" }),
  ];

  for (const file of invalidFiles) {
    await dispatch(() => input.props.onChange({ currentTarget: { files: [file] } }));
    assert.equal(button("Tải ảnh lên"), undefined);
    assert.ok(view!.root.findByProps({ role: "alert" }));
  }
  assert.equal(calls.length, 0);
});

test("primary and delete actions use the latest RowVersion and keep existing images intact until requested", async () => {
  const existingImage: RecipeImage = {
    id: "image-1",
    originalUrl: imageResponse.originalUrl,
    mediumUrl: imageResponse.mediumUrl,
    thumbnailUrl: imageResponse.thumbnailUrl,
    altText: imageResponse.altText,
    isPrimary: false,
    orderIndex: 0,
  };
  await dispatch(() => { view = create(<ManagerHost initialImages={[existingImage]} />); });
  assert.match(text(view!.root), /Ảnh bổ sung/);
  assert.equal(calls.length, 0);

  await dispatch(() => button("Đặt làm ảnh đại diện").props.onClick());
  const primaryRequest = calls.at(0);
  assert.ok(primaryRequest);
  assert.equal(primaryRequest.method, "PATCH");
  assert.deepEqual(JSON.parse(String(primaryRequest.body)), { rowVersion });
  assert.match(text(view!.root), /Ảnh đại diện/);

  await dispatch(() => button("Xóa ảnh").props.onClick());
  const deleteRequest = calls.at(1);
  assert.ok(deleteRequest);
  assert.equal(deleteRequest.method, "DELETE");
  assert.deepEqual(JSON.parse(String(deleteRequest.body)), { rowVersion: imageResponse.rowVersion });
  assert.equal(view!.root.findByProps({ role: "img" }).props["aria-label"],
    "Ảnh minh họa cho Chưa có ảnh công thức; chưa có ảnh món ăn");
});
