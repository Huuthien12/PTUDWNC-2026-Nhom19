"use client";

import { type FormEvent, useCallback, useEffect, useRef, useState } from "react";
import { ApiError } from "@/services/api-client";
import { getProfile, updateProfile } from "@/services/profile-service";
import type { UserProfile } from "@/types/profile";

function errorMessage(error: unknown) {
  if (error instanceof ApiError && error.status === 401) return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
  return "Không thể cập nhật hồ sơ. Vui lòng thử lại.";
}

export default function ProfileForm() {
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [fullName, setFullName] = useState("");
  const [avatarUrl, setAvatarUrl] = useState("");
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [loading, setLoading] = useState(true);
  const [pending, setPending] = useState(false);
  const submitting = useRef(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const current = await getProfile();
      setProfile(current);
      setFullName(current.fullName);
      setAvatarUrl(current.avatarUrl ?? "");
    } catch (loadError) {
      setError(errorMessage(loadError));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void load(); }, [load]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting.current) return;

    const name = fullName.trim();
    const avatar = avatarUrl.trim();
    setError("");
    setSuccess("");
    if (name.length < 2 || name.length > 100) {
      setError("Họ tên phải có từ 2 đến 100 ký tự.");
      return;
    }
    if (avatar && !URL.canParse(avatar)) {
      setError("URL ảnh đại diện không hợp lệ.");
      return;
    }

    submitting.current = true;
    setPending(true);
    try {
      const updated = await updateProfile({ fullName: name, ...(avatar ? { avatarUrl: avatar } : {}) });
      setProfile(updated);
      setFullName(updated.fullName);
      setAvatarUrl(updated.avatarUrl ?? "");
      setSuccess("Đã cập nhật hồ sơ.");
    } catch (updateError) {
      setError(errorMessage(updateError));
    } finally {
      submitting.current = false;
      setPending(false);
    }
  }

  if (loading) return <main className="cb-container cb-section" role="status">Đang tải hồ sơ...</main>;
  if (!profile) return <main className="cb-container cb-section" role="alert"><p>{error}</p><button type="button" className="mt-4 underline" onClick={() => void load()}>Thử lại</button></main>;

  return <main className="cb-container cb-section max-w-2xl">
    <h1 className="cb-section-title">Hồ sơ cá nhân</h1>
    <p className="mt-2 text-sm text-gray-600">Cập nhật thông tin hiển thị của bạn.</p>
    {error && <p role="alert" className="mt-4 rounded border border-red-200 bg-red-50 p-3 text-red-700">{error}</p>}
    {success && <p role="status" className="mt-4 rounded border border-green-200 bg-green-50 p-3 text-green-800">{success}</p>}

    <form noValidate aria-busy={pending} onSubmit={submit} className="mt-6 space-y-5 rounded-xl border bg-white p-6">
      <fieldset disabled={pending} className="space-y-5 disabled:opacity-60">
        <div>
          <label htmlFor="fullName" className="mb-1 block font-medium">Họ tên</label>
          <input id="fullName" value={fullName} onChange={(event) => setFullName(event.target.value)}
            className="w-full rounded-lg border border-gray-300 p-3" />
        </div>
        <div>
          <label htmlFor="avatarUrl" className="mb-1 block font-medium">URL ảnh đại diện</label>
          <input id="avatarUrl" type="url" value={avatarUrl} onChange={(event) => setAvatarUrl(event.target.value)}
            placeholder="https://example.com/avatar.jpg" className="w-full rounded-lg border border-gray-300 p-3" />
        </div>
        <dl className="grid gap-3 rounded-lg bg-gray-50 p-4 text-sm sm:grid-cols-2">
          <div><dt className="font-medium">Email</dt><dd>{profile.email}</dd></div>
          <div><dt className="font-medium">Tên người dùng</dt><dd>{profile.userName}</dd></div>
          <div><dt className="font-medium">Vai trò</dt><dd>{profile.roles.join(", ")}</dd></div>
          <div><dt className="font-medium">Xác thực email</dt><dd>{profile.emailConfirmed ? "Đã xác thực" : "Chưa xác thực"}</dd></div>
        </dl>
        <button type="submit" className="rounded-lg bg-[#254b3b] px-4 py-2 font-semibold text-white disabled:opacity-50">
          {pending ? "Đang lưu..." : "Lưu thay đổi"}
        </button>
      </fieldset>
    </form>
  </main>;
}
