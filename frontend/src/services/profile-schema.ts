import { z } from "zod";

export const profileSchema = z.object({
  fullName: z.string().trim().min(2, "Họ tên phải có từ 2 đến 100 ký tự.").max(100, "Họ tên phải có từ 2 đến 100 ký tự."),
  avatarUrl: z.string().trim().refine(value => !value || URL.canParse(value), "URL ảnh đại diện không hợp lệ."),
});

export type ProfileFields = z.infer<typeof profileSchema>;
