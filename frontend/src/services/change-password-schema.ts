import { z } from "zod";

const password = z.string()
  .min(8, "Mật khẩu cần ít nhất 8 ký tự.")
  .regex(/[A-Z]/, "Mật khẩu cần có chữ hoa.")
  .regex(/[a-z]/, "Mật khẩu cần có chữ thường.")
  .regex(/[0-9]/, "Mật khẩu cần có chữ số.")
  .regex(/[\W_]/, "Mật khẩu cần có ký tự đặc biệt.");

export const changePasswordSchema = z.object({
  currentPassword: z.string().min(1, "Vui lòng nhập mật khẩu hiện tại."),
  newPassword: password,
  confirmNewPassword: z.string().min(1, "Vui lòng xác nhận mật khẩu mới."),
}).refine((value) => value.newPassword === value.confirmNewPassword, {
  message: "Xác nhận mật khẩu mới không khớp.",
  path: ["confirmNewPassword"],
});

export type ChangePasswordFields = z.infer<typeof changePasswordSchema>;
