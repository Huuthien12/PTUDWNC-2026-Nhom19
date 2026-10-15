import assert from "node:assert/strict";
import { beforeEach, test } from "node:test";
import { act, create, type ReactTestInstance } from "react-test-renderer";
import ForgotPasswordForm from "../src/components/auth/ForgotPasswordForm";
import ResetPasswordForm from "../src/components/auth/ResetPasswordForm";
import { resetPasswordSchema } from "../src/services/password-recovery-schema";

Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });

let calls: { url: string; options: RequestInit }[];
let respond: () => Promise<Response>;

beforeEach(() => {
  calls = [];
  respond = async () => Response.json({}, { status: 202 });
  globalThis.fetch = async (input, options = {}) => {
    calls.push({ url: String(input), options });
    return respond();
  };
});

function content(node: ReactTestInstance): string {
  return node.children.map((child) => typeof child === "string" ? child : content(child)).join("");
}

async function render(component: React.ReactElement) {
  let view!: ReturnType<typeof create>;
  await act(async () => { view = create(component); });
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

test("forgot-password renders and sends only the email with generic success", async () => {
  const view = await render(<ForgotPasswordForm />);
  await fill(view, "email", " author@example.test ");
  await submit(view);

  assert.equal(calls.length, 1);
  assert.match(calls[0].url, /\/api\/v1\/auth\/forgot-password$/);
  assert.equal(calls[0].options.body, JSON.stringify({ email: "author@example.test" }));
  assert.match(content(view.root), /Nếu email tồn tại trong hệ thống/);
  assert.doesNotMatch(content(view.root), /Email không tồn tại/);
});

test("forgot-password validation blocks an invalid email", async () => {
  const view = await render(<ForgotPasswordForm />);
  await fill(view, "email", "invalid");
  await submit(view);
  assert.equal(calls.length, 0);
  assert.match(content(view.root), /Email không hợp lệ/);
});

test("forgot-password prevents duplicate pending requests and hides server detail", async () => {
  let resolve!: (response: Response) => void;
  respond = () => new Promise<Response>((done) => { resolve = done; });
  const view = await render(<ForgotPasswordForm />);
  await fill(view, "email", "author@example.test");
  const form = view.root.findByType("form");
  let first!: Promise<void>;
  await act(async () => {
    first = form.props.onSubmit({ preventDefault() {} });
    await Promise.resolve();
  });
  assert.equal(calls.length, 1);
  assert.equal(view.root.findByType("fieldset").props.disabled, true);
  await act(async () => {
    void form.props.onSubmit({ preventDefault() {} });
    await Promise.resolve();
  });
  assert.equal(calls.length, 1);
  resolve(Response.json({ type: "INTERNAL_SERVER_ERROR", detail: "private detail" }, { status: 500 }));
  await act(async () => { await first; });
  assert.doesNotMatch(content(view.root), /private detail/);
});

test("reset-password rejects a missing or malformed link without rendering the token", async () => {
  const missing = await render(<ResetPasswordForm />);
  assert.equal(missing.root.findAllByType("form").length, 0);
  assert.match(content(missing.root), /Liên kết đặt lại mật khẩu không hợp lệ/);

  const token = "token_test-123";
  const valid = await render(<ResetPasswordForm email="author@example.test" token={token} />);
  assert.equal(valid.root.findAllByType("form").length, 1);
  assert.doesNotMatch(content(valid.root), new RegExp(token));
});

test("reset-password schema rejects weak and mismatched passwords", () => {
  assert.equal(resetPasswordSchema.safeParse({ newPassword: "weak", confirmNewPassword: "weak" }).success, false);
  assert.equal(resetPasswordSchema.safeParse({
    newPassword: "New-password@456", confirmNewPassword: "Other-password@789",
  }).success, false);
});

test("reset-password posts the URL identity fields, clears passwords and links to login", async () => {
  respond = async () => new Response(null, { status: 204 });
  const view = await render(<ResetPasswordForm email="author@example.test" token="token_test-123" />);
  await fill(view, "newPassword", "New-password@456");
  await fill(view, "confirmNewPassword", "New-password@456");
  await submit(view);

  assert.equal(calls.length, 1);
  assert.match(calls[0].url, /\/api\/v1\/auth\/reset-password$/);
  assert.equal(calls[0].options.body, JSON.stringify({
    email: "author@example.test",
    token: "token_test-123",
    newPassword: "New-password@456",
    confirmNewPassword: "New-password@456",
  }));
  assert.equal(view.root.findAllByType("input").length, 0);
  assert.match(content(view.root), /Đã đặt lại mật khẩu thành công/);
  assert.ok(view.root.findAll((node) => node.props.href === "/login").length > 0);
});

test("reset-password prevents duplicate requests and shows a safe invalid-token error", async () => {
  let resolve!: (response: Response) => void;
  respond = () => new Promise<Response>((done) => { resolve = done; });
  const view = await render(<ResetPasswordForm email="author@example.test" token="token_test-123" />);
  await fill(view, "newPassword", "New-password@456");
  await fill(view, "confirmNewPassword", "New-password@456");
  const form = view.root.findByType("form");
  let first!: Promise<void>;
  await act(async () => {
    first = form.props.onSubmit({ preventDefault() {} });
    void form.props.onSubmit({ preventDefault() {} });
    await Promise.resolve();
  });
  assert.equal(calls.length, 1);
  resolve(Response.json({
    type: "RESET_PASSWORD_INVALID_OR_EXPIRED",
    detail: "token internals must stay hidden",
  }, { status: 400 }));
  await act(async () => { await first; });
  assert.match(content(view.root), /không hợp lệ hoặc đã hết hạn/);
  assert.doesNotMatch(content(view.root), /token internals/);
});
