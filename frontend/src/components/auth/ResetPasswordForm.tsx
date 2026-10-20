"use client";

import { useRef, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm, useWatch, type UseFormRegisterReturn } from "react-hook-form";
import { ApiError } from "@/services/api-client";
import { resetPassword } from "@/services/auth-service";
import { resetPasswordSchema, type ResetPasswordFields } from "@/services/password-recovery-schema";

function errorMessage(error: unknown) {
  if (error instanceof ApiError) {
    if (error.problem?.type === "RESET_PASSWORD_INVALID_OR_EXPIRED")
      return "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.";
    if (error.status === 400) return "Không thể đặt lại mật khẩu. Vui lòng kiểm tra thông tin.";
    if (error.status === 429) return "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau.";
  }
  return "Không thể đặt lại mật khẩu lúc này. Vui lòng thử lại.";
}

export default function ResetPasswordForm({ email, token }: { email?: string; token?: string }) {
  const pending = useRef(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(false);
  const { register, handleSubmit, reset, control, formState: { errors, isSubmitting } } = useForm<ResetPasswordFields>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { newPassword: "", confirmNewPassword: "" },
  });
  const values = useWatch({ control });

  if (!email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) ||
      !token || token.length > 4096 || !/^[A-Za-z0-9_-]+$/.test(token))
    return <main className="cb-container cb-section max-w-2xl">
    <h1 className="cb-section-title">Liên kết không hợp lệ</h1>
    <p role="alert" className="mt-4 text-red-700">Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.</p>
    <a href="/forgot-password" className="mt-4 inline-block text-sm font-medium text-[#254b3b] underline">Yêu cầu liên kết mới</a>
  </main>;

  // The callback runs only on submit; the ref prevents same-tick duplicate requests.
  // eslint-disable-next-line react-hooks/refs
  const submit = handleSubmit(async (fields) => {
    if (pending.current) return;
    pending.current = true;
    setError("");
    try {
      await resetPassword({ email, token, ...fields });
      reset();
      setSuccess(true);
    } catch (submitError) {
      setError(errorMessage(submitError));
    } finally {
      pending.current = false;
    }
  });

  return <main className="cb-container cb-section max-w-2xl">
    <h1 className="cb-section-title">Đặt lại mật khẩu</h1>
    {error && <p role="alert" className="mt-4 rounded border border-red-200 bg-red-50 p-3 text-red-700">{error}</p>}
    {success ? <div className="mt-6 rounded border border-green-200 bg-green-50 p-4 text-green-800">
      <p role="status">Đã đặt lại mật khẩu thành công.</p>
      <a href="/login" className="mt-3 inline-block font-medium underline">Đăng nhập</a>
    </div> : <form noValidate aria-busy={isSubmitting} onSubmit={submit} className="mt-6 space-y-5 rounded-xl border bg-white p-6">
      <fieldset disabled={isSubmitting} className="space-y-5 disabled:opacity-60">
        <PasswordField id="newPassword" label="Mật khẩu mới" value={values.newPassword ?? ""}
          error={errors.newPassword?.message} registration={register("newPassword")} />
        <PasswordField id="confirmNewPassword" label="Xác nhận mật khẩu mới" value={values.confirmNewPassword ?? ""}
          error={errors.confirmNewPassword?.message} registration={register("confirmNewPassword")} />
        <button type="submit" disabled={isSubmitting}
          className="rounded-lg bg-[#254b3b] px-4 py-2 font-semibold text-white disabled:opacity-50">
          {isSubmitting ? "Đang đặt lại..." : "Đặt lại mật khẩu"}
        </button>
      </fieldset>
    </form>}
  </main>;
}

function PasswordField({ id, label, value, error, registration }: {
  id: string;
  label: string;
  value: string;
  error?: string;
  registration: UseFormRegisterReturn;
}) {
  return <div>
    <label htmlFor={id} className="mb-1 block font-medium">{label}</label>
    <input id={id} type="password" autoComplete="new-password" value={value} {...registration}
      className="w-full rounded-lg border border-gray-300 p-3" />
    {error && <p role="alert" className="text-sm text-red-700">{error}</p>}
  </div>;
}
