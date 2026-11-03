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
import AuthNotice from "@/components/auth/AuthNotice";

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

  if (profileQuery.isPending) return <main className="cb-container cb-section" role="status"><div className="cb-state">Đang tải hồ sơ...</div></main>;
  if (!profileQuery.data) return <main className="cb-container cb-section" role="alert"><div className="cb-state"><h1 className="cb-section-title">Chưa tải được hồ sơ</h1><p>{errorMessage(profileQuery.error)}</p><button type="button" className="cb-button" onClick={() => void profileQuery.refetch()}>Thử lại</button></div></main>;
  const profile = profileQuery.data;
  const initials = profile.fullName.trim().split(/\s+/).slice(-2).map(part => part[0]).join("").toLocaleUpperCase("vi-VN") || "CB";
  const createdDate = profile.createdAt.slice(0, 10).split("-").reverse().join("/");

  return <main className="cb-container cb-section max-w-5xl">
    <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div>
        <p className="cb-eyebrow mb-2">Tài khoản của bạn</p>
        <h1 className="cb-section-title text-3xl font-bold text-[#254b3b] sm:text-4xl">Hồ sơ cá nhân</h1>
        <p className="mt-2 text-sm text-muted">Quản lý thông tin hiển thị trên Culinary Blog.</p>
      </div>
      <Link href="/profile/change-password" className="cb-button cb-button-secondary">Đổi mật khẩu</Link>
    </div>

    <section className="mb-8 flex flex-col gap-5 rounded-xl border border-line bg-[#dce4d3] p-6 sm:flex-row sm:items-center sm:p-8" aria-label="Tóm tắt hồ sơ">
      {profile.avatarUrl ? (
        <img src={profile.avatarUrl} alt={`Ảnh đại diện của ${profile.fullName}`} referrerPolicy="no-referrer" className="h-24 w-24 shrink-0 rounded-full border-4 border-white object-cover shadow-sm" />
      ) : (
        <div aria-label={`Ảnh đại diện chữ viết tắt ${initials}`} role="img" className="flex h-24 w-24 shrink-0 items-center justify-center rounded-full border-4 border-white bg-[#91aa82] text-2xl font-bold text-[#254b3b] shadow-sm">{initials}</div>
      )}
      <div className="min-w-0 flex-1">
        <p className="cb-eyebrow mb-1">Thành viên Culinary Blog</p>
        <h2 className="break-words text-2xl font-bold tracking-tight text-[#254b3b]">{profile.fullName}</h2>
        <p className="mt-1 break-all text-sm text-[#58634e]">@{profile.userName} <span className="px-1">·</span> {profile.email}</p>
      </div>
      <span className={`w-fit rounded-full px-3 py-1.5 text-xs font-semibold ${profile.emailConfirmed ? "bg-[#edf4e8] text-[#315d3e]" : "bg-[#fff4cc] text-[#76580f]"}`}>
        {profile.emailConfirmed ? "Email đã xác thực" : "Email chưa xác thực"}
      </span>
    </section>

    {error && <AuthNotice kind="error" className="mb-5 rounded-md border border-[#e7c3b8] bg-[#fcf0ec] px-4 py-3 text-sm text-[#922f2b]" onDismiss={() => setError("")}>{error}</AuthNotice>}
    {success && <AuthNotice kind="success" className="mb-5 rounded-md border border-[#b9cfad] bg-[#edf4e8] px-4 py-3 text-sm text-[#254b3b]" onDismiss={() => setSuccess("")}>{success}</AuthNotice>}

    <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_280px]">
      <form noValidate aria-busy={save.isPending} onSubmit={submit} className="rounded-lg border border-line bg-white p-6 sm:p-8">
        <fieldset disabled={save.isPending} className="space-y-6 disabled:opacity-60">
          <div>
            <h2 className="text-xl font-bold text-[#254b3b]">Thông tin hiển thị</h2>
            <p className="mt-1 text-sm text-muted">Tên và ảnh đại diện sẽ xuất hiện bên cạnh nội dung bạn chia sẻ.</p>
          </div>
          <div className="cb-field !mt-0">
            <label htmlFor="fullName">Họ và tên</label>
            <input id="fullName" autoComplete="name" {...register("fullName")} className="cb-input" />
            {errors.fullName && <p role="alert" className="mt-2 text-sm text-[#922f2b]">{errors.fullName.message}</p>}
          </div>
          <div className="cb-field !mt-0">
            <label htmlFor="avatarUrl">Đường dẫn ảnh đại diện</label>
            <input id="avatarUrl" type="url" autoComplete="url" {...register("avatarUrl")} placeholder="https://example.com/avatar.jpg" className="cb-input" />
            <p className="cb-field-hint">Dán đường dẫn ảnh công khai để dùng ảnh đại diện.</p>
            {errors.avatarUrl && <p role="alert" className="mt-2 text-sm text-[#922f2b]">{errors.avatarUrl.message}</p>}
          </div>
          <div className="flex flex-wrap items-center gap-3 border-t border-line pt-5">
            <button type="submit" className="cb-button disabled:opacity-50">
              {save.isPending ? "Đang lưu..." : "Lưu thay đổi"}
            </button>
            <span className="text-xs text-muted">Thành viên từ {createdDate}</span>
          </div>
        </fieldset>
      </form>

      <aside className="rounded-lg border border-line bg-white p-6" aria-labelledby="account-details-heading">
        <h2 id="account-details-heading" className="text-lg font-bold text-[#254b3b]">Thông tin tài khoản</h2>
        <dl className="mt-4 divide-y divide-line text-sm">
          <div className="py-3"><dt className="text-muted">Email</dt><dd className="mt-1 break-all font-semibold">{profile.email}</dd></div>
          <div className="py-3"><dt className="text-muted">Tên người dùng</dt><dd className="mt-1 font-semibold">{profile.userName}</dd></div>
          <div className="py-3"><dt className="text-muted">Vai trò</dt><dd className="mt-1 font-semibold">{profile.roles.join(", ") || "Thành viên"}</dd></div>
        </dl>
        <Link href="/profile/change-password" className="mt-4 inline-flex text-sm font-semibold text-[#254b3b] underline decoration-[#c9ad4a] underline-offset-4">Bảo mật tài khoản</Link>
      </aside>
    </div>
  </main>;
}
