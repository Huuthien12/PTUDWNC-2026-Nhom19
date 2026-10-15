"use client";

import { type FormEvent, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { ApiError } from "@/services/api-client";
import { getSession } from "@/services/auth-session";
import { getProfile, updateProfile } from "@/services/profile-service";
import { profileSchema, type ProfileFields } from "@/services/profile-schema";

function errorMessage(error: unknown) {
  if (error instanceof ApiError && error.status === 401) return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
  return "Không thể cập nhật hồ sơ. Vui lòng thử lại.";
}

export default function ProfileForm() {
  const sessionId = getSession()?.sessionId;
  const key = ["profile", sessionId];
  const queryClient = useQueryClient();
  const profileQuery = useQuery({ queryKey: key, queryFn: getProfile, retry: false, refetchOnWindowFocus: false });
  const { register, handleSubmit, reset, formState: { errors } } = useForm<ProfileFields>({
    resolver: zodResolver(profileSchema),
    defaultValues: { fullName: "", avatarUrl: "" },
  });
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const submitting = useRef(false);
  const save = useMutation({ mutationFn: updateProfile });

  useEffect(() => {
    if (profileQuery.data) reset({ fullName: profileQuery.data.fullName, avatarUrl: profileQuery.data.avatarUrl ?? "" });
  }, [profileQuery.data, reset]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting.current) return;
    submitting.current = true;
    try {
      await handleSubmit(async fields => {
        setError("");
        setSuccess("");
        try {
          const updated = await save.mutateAsync({
            fullName: fields.fullName,
            ...(fields.avatarUrl ? { avatarUrl: fields.avatarUrl } : {}),
          });
          queryClient.setQueryData(key, updated);
          reset({ fullName: updated.fullName, avatarUrl: updated.avatarUrl ?? "" });
          setSuccess("Đã cập nhật hồ sơ.");
        } catch (updateError) {
          setError(errorMessage(updateError));
        }
      })(event);
    } finally {
      submitting.current = false;
    }
  }

  if (profileQuery.isPending) return <main className="cb-container cb-section" role="status">Đang tải hồ sơ...</main>;
  if (!profileQuery.data) return <main className="cb-container cb-section" role="alert"><p>{errorMessage(profileQuery.error)}</p><button type="button" className="mt-4 underline" onClick={() => void profileQuery.refetch()}>Thử lại</button></main>;
  const profile = profileQuery.data;

  return <main className="cb-container cb-section max-w-2xl">
    <h1 className="cb-section-title">Hồ sơ cá nhân</h1>
    <p className="mt-2 text-sm text-gray-600">Cập nhật thông tin hiển thị của bạn.</p>
    <Link href="/profile/change-password" className="mt-3 inline-block text-sm font-medium text-[#254b3b] underline">Đổi mật khẩu</Link>
    {error && <p role="alert" className="mt-4 rounded border border-red-200 bg-red-50 p-3 text-red-700">{error}</p>}
    {success && <p role="status" className="mt-4 rounded border border-green-200 bg-green-50 p-3 text-green-800">{success}</p>}

    <form noValidate aria-busy={save.isPending} onSubmit={submit} className="mt-6 space-y-5 rounded-xl border bg-white p-6">
      <fieldset disabled={save.isPending} className="space-y-5 disabled:opacity-60">
        <div>
          <label htmlFor="fullName" className="mb-1 block font-medium">Họ tên</label>
          <input id="fullName" {...register("fullName")} className="w-full rounded-lg border border-gray-300 p-3" />
          {errors.fullName && <p role="alert" className="text-sm text-red-700">{errors.fullName.message}</p>}
        </div>
        <div>
          <label htmlFor="avatarUrl" className="mb-1 block font-medium">URL ảnh đại diện</label>
          <input id="avatarUrl" type="url" {...register("avatarUrl")}
            placeholder="https://example.com/avatar.jpg" className="w-full rounded-lg border border-gray-300 p-3" />
          {errors.avatarUrl && <p role="alert" className="text-sm text-red-700">{errors.avatarUrl.message}</p>}
        </div>
        <dl className="grid gap-3 rounded-lg bg-gray-50 p-4 text-sm sm:grid-cols-2">
          <div><dt className="font-medium">Email</dt><dd>{profile.email}</dd></div>
          <div><dt className="font-medium">Tên người dùng</dt><dd>{profile.userName}</dd></div>
          <div><dt className="font-medium">Vai trò</dt><dd>{profile.roles.join(", ")}</dd></div>
          <div><dt className="font-medium">Xác thực email</dt><dd>{profile.emailConfirmed ? "Đã xác thực" : "Chưa xác thực"}</dd></div>
        </dl>
        <button type="submit" className="rounded-lg bg-[#254b3b] px-4 py-2 font-semibold text-white disabled:opacity-50">
          {save.isPending ? "Đang lưu..." : "Lưu thay đổi"}
        </button>
      </fieldset>
    </form>
  </main>;
}
