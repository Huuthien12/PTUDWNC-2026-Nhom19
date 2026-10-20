import assert from "node:assert/strict";
import { beforeEach, test } from "node:test";
import { act, create, type ReactTestInstance } from "react-test-renderer";
import ChangePasswordForm from "../src/components/auth/ChangePasswordForm";
import { changePasswordSchema } from "../src/services/change-password-schema";
import { SESSION_KEY } from "../src/services/auth-session";

Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });

class MemoryStorage {
  private values = new Map<string, string>();
  getItem(key: string) { return this.values.get(key) ?? null; }
  setItem(key: string, value: string) { this.values.set(key, value); }
  removeItem(key: string) { this.values.delete(key); }
}

class Browser extends EventTarget {
  localStorage = new MemoryStorage();
}

let browser: Browser;
let calls: { url: string; options: RequestInit }[];
let respond: () => Promise<Response>;

beforeEach(() => {
  browser = new Browser();
  calls = [];
  Object.defineProperty(globalThis, "window", { value: browser, configurable: true });
  browser.localStorage.setItem(SESSION_KEY, JSON.stringify({
    accessToken: "access-test", refreshToken: "refresh-test", tokenType: "Bearer",
    expiresAt: new Date(Date.now() + 60_000).toISOString(), sessionId: "session-test",
    user: { id: "user-1", fullName: "Author", userName: "author", email: "author@example.test", avatarUrl: null, roles: ["Author"] },
  }));
  respond = async () => new Response(null, { status: 204 });
  globalThis.fetch = async (input, options = {}) => {
    calls.push({ url: String(input), options });
    return respond();
  };
});

function content(node: ReactTestInstance): string {
  return node.children.map((child) => typeof child === "string" ? child : content(child)).join("");
}

async function render() {
  let view!: ReturnType<typeof create>;
  await act(async () => { view = create(<ChangePasswordForm />); });
  return view;
}

async function fill(view: ReturnType<typeof create>, id: string, value: string) {
  await act(async () => {
    const input = view.root.find((node) => node.type === "input" && node.props.id === id);
    input.props.onChange({ target: { name: input.props.name, value } });
  });
}

async function submit(view: ReturnType<typeof create>) {
  await act(async () => { await view.root.findByType("form").props.onSubmit({ preventDefault() {} }); });
}

test("change-password validation rejects a mismatch before an API request", async () => {
  const view = await render();
  await fill(view, "currentPassword", "Old-password@123");
  await fill(view, "newPassword", "New-password@456");
  await fill(view, "confirmNewPassword", "Other-password@789");
  await submit(view);
  assert.match(content(view.root), /Xác nhận mật khẩu mới không khớp/);
  assert.equal(calls.length, 0);
});

test("change-password posts only password fields with the existing bearer and clears on success", async () => {
  const view = await render();
  for (const [id, value] of Object.entries({
    currentPassword: "Old-password@123", newPassword: "New-password@456", confirmNewPassword: "New-password@456",
  })) await fill(view, id, value);
  await submit(view);
  assert.equal(calls.length, 1);
  assert.match(calls[0].url, /\/api\/v1\/auth\/change-password$/);
  assert.equal(calls[0].options.method, "POST");
  assert.equal(new Headers(calls[0].options.headers).get("Authorization"), "Bearer access-test");
  assert.equal(calls[0].options.body, JSON.stringify({
    currentPassword: "Old-password@123", newPassword: "New-password@456", confirmNewPassword: "New-password@456",
  }));
  assert.match(content(view.root), /Đã đổi mật khẩu thành công/);
  for (const id of ["currentPassword", "newPassword", "confirmNewPassword"]) {
    const input = view.root.find((node) => node.type === "input" && node.props.id === id);
    assert.equal(input.props.type, "password");
    assert.equal(input.props.value, "");
  }
});

test("change-password shows a safe API error without exposing server detail", async () => {
  respond = async () => Response.json({ type: "INVALID_CURRENT_PASSWORD", detail: "never show this" }, { status: 400 });
  const view = await render();
  for (const [id, value] of Object.entries({
    currentPassword: "Old-password@123", newPassword: "New-password@456", confirmNewPassword: "New-password@456",
  })) await fill(view, id, value);
  await submit(view);
  assert.match(content(view.root), /Mật khẩu hiện tại không đúng/);
  assert.doesNotMatch(content(view.root), /never show this/);
  assert.equal(calls.length, 1);
  assert.equal(view.root.find((node) => node.type === "input" && node.props.id === "currentPassword").props.value, "Old-password@123");
});

test("change-password disables the form while submission is pending", async () => {
  let resolve!: (response: Response) => void;
  respond = () => new Promise<Response>((done) => { resolve = done; });
  const view = await render();
  for (const [id, value] of Object.entries({
    currentPassword: "Old-password@123", newPassword: "New-password@456", confirmNewPassword: "New-password@456",
  })) await fill(view, id, value);
  const form = view.root.findByType("form");
  let first!: Promise<void>;
  await act(async () => { first = form.props.onSubmit({ preventDefault() {} }); await Promise.resolve(); });
  assert.equal(calls.length, 1);
  assert.equal(view.root.findByType("fieldset").props.disabled, true);
  resolve(new Response(null, { status: 204 }));
  await act(async () => { await first; });
});

test("change-password schema follows the backend password policy", () => {
  assert.equal(changePasswordSchema.safeParse({
    currentPassword: "old", newPassword: "New-password@456", confirmNewPassword: "New-password@456",
  }).success, true);
  assert.equal(changePasswordSchema.safeParse({
    currentPassword: "old", newPassword: "weak", confirmNewPassword: "weak",
  }).success, false);
});
