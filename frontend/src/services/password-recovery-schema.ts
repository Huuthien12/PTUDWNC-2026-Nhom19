import { z } from "zod";
import { passwordSchema } from "./change-password-schema";

export const forgotPasswordSchema = z.object({
  email: z.string().trim().email("Email không hợp lệ."),
});

export const resetPasswordSchema = z.object({
  newPassword: passwordSchema,
  confirmNewPassword: z.string().min(1, "Vui lòng xác nhận mật khẩu mới."),
}).refine((value) => value.newPassword === value.confirmNewPassword, {
  message: "Xác nhận mật khẩu mới không khớp.",
  path: ["confirmNewPassword"],
});

export type ForgotPasswordFields = z.infer<typeof forgotPasswordSchema>;
export type ResetPasswordFields = z.infer<typeof resetPasswordSchema>;
