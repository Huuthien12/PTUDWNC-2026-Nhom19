"use client";

import { useRef, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm, type UseFormRegisterReturn } from "react-hook-form";
import { ApiError } from "@/services/api-client";
import { changePassword } from "@/services/auth-service";
import { changePasswordSchema, type ChangePasswordFields } from "@/services/change-password-schema";

function errorMessage(error: unknown) {
  if (error instanceof ApiError) {
    if (error.status === 401) return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
    if (error.status === 423) return "Tài khoản đang tạm khóa. Vui lòng thử lại sau.";
    if (error.problem?.type === "INVALID_CURRENT_PASSWORD") return "Mật khẩu hiện tại không đúng.";
    if (error.status === 400) return "Không thể đổi mật khẩu. Vui lòng kiểm tra lại thông tin.";
  }
  return "Không thể đổi mật khẩu lúc này. Vui lòng thử lại.";
}

export default function ChangePasswordForm() {
  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<ChangePasswordFields>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: "", newPassword: "", confirmNewPassword: "" },
  });
  const submitting = useRef(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const submit = handleSubmit(async (fields) => {
    if (submitting.current) return;
    submitting.current = true;
    setError("");
    setSuccess("");
    try {
      await changePassword(fields);
      reset();
      setSuccess("Đã đổi mật khẩu thành công.");
    } catch (submitError) {
      setError(errorMessage(submitError));
    } finally {
      submitting.current = false;
    }
  });

  return <main className="cb-container cb-section max-w-2xl">
    <h1 className="cb-section-title">Đổi mật khẩu</h1>
    <p className="mt-2 text-sm text-gray-600">Nhập mật khẩu hiện tại và mật khẩu mới của bạn.</p>
    {error && <p role="alert" className="mt-4 rounded border border-red-200 bg-red-50 p-3 text-red-700">{error}</p>}
    {success && <p role="status" className="mt-4 rounded border border-green-200 bg-green-50 p-3 text-green-800">{success}</p>}
    <form noValidate aria-busy={isSubmitting} onSubmit={submit} className="mt-6 space-y-5 rounded-xl border bg-white p-6">
      <fieldset disabled={isSubmitting} className="space-y-5 disabled:opacity-60">
        <PasswordField id="currentPassword" label="Mật khẩu hiện tại" autoComplete="current-password" error={errors.currentPassword?.message}
          registration={register("currentPassword")} />
        <PasswordField id="newPassword" label="Mật khẩu mới" autoComplete="new-password" error={errors.newPassword?.message}
          registration={register("newPassword")} />
        <PasswordField id="confirmNewPassword" label="Xác nhận mật khẩu mới" autoComplete="new-password" error={errors.confirmNewPassword?.message}
          registration={register("confirmNewPassword")} />
        <button type="submit" className="rounded-lg bg-[#254b3b] px-4 py-2 font-semibold text-white disabled:opacity-50">
          {isSubmitting ? "Đang đổi mật khẩu..." : "Đổi mật khẩu"}
        </button>
      </fieldset>
    </form>
  </main>;
}

function PasswordField({ id, label, autoComplete, error, registration }: {
  id: string;
  label: string;
  autoComplete: string;
  error?: string;
  registration: UseFormRegisterReturn;
}) {
  return <div>
    <label htmlFor={id} className="mb-1 block font-medium">{label}</label>
    <input id={id} type="password" autoComplete={autoComplete} {...registration}
      className="w-full rounded-lg border border-gray-300 p-3" />
    {error && <p role="alert" className="text-sm text-red-700">{error}</p>}
  </div>;
}
