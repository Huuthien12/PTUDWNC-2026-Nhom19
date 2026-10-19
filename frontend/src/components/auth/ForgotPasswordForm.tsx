"use client";

import { useRef, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { ApiError } from "@/services/api-client";
import { forgotPassword } from "@/services/auth-service";
import { forgotPasswordSchema, type ForgotPasswordFields } from "@/services/password-recovery-schema";

const successMessage = "Nếu email tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu đã được gửi.";

function errorMessage(error: unknown) {
  if (error instanceof ApiError) {
    if (error.status === 400) return "Email không hợp lệ.";
    if (error.status === 429) return "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau.";
  }
  return "Không thể gửi yêu cầu lúc này. Vui lòng thử lại.";
}

export default function ForgotPasswordForm() {
  const pending = useRef(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<ForgotPasswordFields>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: { email: "" },
  });

  const submit = handleSubmit(async (fields) => {
    if (pending.current) return;
    pending.current = true;
    setError("");
    setSuccess("");
    try {
      await forgotPassword({ email: fields.email.trim() });
      reset();
      setSuccess(successMessage);
    } catch (submitError) {
      setError(errorMessage(submitError));
    } finally {
      pending.current = false;
    }
  });

  return <main className="cb-container cb-section max-w-2xl">
    <h1 className="cb-section-title">Quên mật khẩu</h1>
    <p className="mt-2 text-sm text-gray-600">Nhập email để nhận hướng dẫn đặt lại mật khẩu.</p>
    {error && <p role="alert" className="mt-4 rounded border border-red-200 bg-red-50 p-3 text-red-700">{error}</p>}
    {success && <p role="status" className="mt-4 rounded border border-green-200 bg-green-50 p-3 text-green-800">{success}</p>}
    <form noValidate aria-busy={isSubmitting} onSubmit={submit} className="mt-6 space-y-5 rounded-xl border bg-white p-6">
      <fieldset disabled={isSubmitting} className="space-y-5 disabled:opacity-60">
        <div>
          <label htmlFor="email" className="mb-1 block font-medium">Email</label>
          <input id="email" type="email" autoComplete="email" {...register("email")}
            className="w-full rounded-lg border border-gray-300 p-3" />
          {errors.email && <p role="alert" className="text-sm text-red-700">{errors.email.message}</p>}
        </div>
        <button type="submit" disabled={isSubmitting}
          className="rounded-lg bg-[#254b3b] px-4 py-2 font-semibold text-white disabled:opacity-50">
          {isSubmitting ? "Đang gửi..." : "Gửi hướng dẫn"}
        </button>
      </fieldset>
    </form>
    <a href="/login" className="mt-4 inline-block text-sm font-medium text-[#254b3b] underline">Quay lại đăng nhập</a>
  </main>;
}
