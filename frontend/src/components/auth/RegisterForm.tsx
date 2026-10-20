"use client";

import { FormEvent, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { register } from "@/services/auth-service";
import AuthNotice from "@/components/auth/AuthNotice";

export default function RegisterForm() {
  const router = useRouter();
  const [form, setForm] = useState({ fullName: "", email: "", userName: "", password: "", confirmPassword: "" });
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState("");

  function updateField(field: keyof typeof form, value: string) {
    setForm((current) => ({ ...current, [field]: value }));
    setError("");
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    setSuccess("");
    if (!form.fullName.trim() || !form.email.trim() || !form.userName.trim() || !form.password || !form.confirmPassword) {
      setError("Vui lòng điền đầy đủ thông tin."); return;
    }
    if (!/^[a-zA-Z0-9]+$/.test(form.userName)) {
      setError("Tên người dùng chỉ gồm chữ cái không dấu và chữ số."); return;
    }
    if (form.password.length < 8 || !/[A-Z]/.test(form.password) || !/[a-z]/.test(form.password) || !/[0-9]/.test(form.password) || !/[\W_]/.test(form.password)) {
      setError("Mật khẩu cần ít nhất 8 ký tự, gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt."); return;
    }
    if (form.password !== form.confirmPassword) {
      setError("Xác nhận mật khẩu không khớp."); return;
    }
    try {
      setLoading(true);
      await register({ fullName: form.fullName.trim(), email: form.email.trim(), userName: form.userName.trim(), password: form.password });
      setSuccess("Đăng ký thành công. Đang chuyển đến trang chủ...");
      window.setTimeout(() => router.push("/"), 1200);
    } catch (err) {
      setError(err instanceof Error && err.message ? err.message : "Không thể kết nối đến máy chủ. Vui lòng thử lại.");
    } finally {
      setLoading(false);
    }
  }

  const inputClass = "h-12 w-full rounded-md border border-[#cbd1c2] bg-white px-3 text-[15px] text-[#28351f] outline-none transition placeholder:text-[#92998b] focus:border-[#526645] focus:ring-2 focus:ring-[#526645]/20 disabled:opacity-60";
  return (
    <section className="relative flex min-h-[calc(100vh-8rem)] items-center overflow-hidden bg-[#f7f5ef] px-4 py-10 sm:px-8 lg:px-12">
      <div className="absolute inset-y-0 left-0 hidden w-[42%] bg-[#dfe8d5] lg:block" />
      <div className="relative z-10 mx-auto grid w-full max-w-6xl overflow-hidden border border-[#d8d8ca] bg-white shadow-[0_18px_60px_rgba(75,82,55,0.12)] lg:grid-cols-[0.9fr_1.1fr]">
        <div className="relative hidden min-h-[620px] bg-cover bg-center lg:block" style={{ backgroundImage: "linear-gradient(180deg, rgba(40,53,27,0.04), rgba(40,53,27,0.55)), url('https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=1000&q=85')" }}>
          <div className="absolute inset-x-8 bottom-9 text-white"><p className="mb-3 text-xs font-semibold uppercase tracking-[0.28em] text-[#e5efcf]">Bếp nhỏ, cảm hứng lớn</p><h2 className="max-w-sm font-serif text-4xl leading-tight">Làm một món ngon.<br />Kể một câu chuyện.</h2></div>
        </div>
        <div className="flex items-center px-6 py-10 sm:px-12 lg:px-16 lg:py-14">
          <div className="mx-auto w-full max-w-md">
            <div className="mb-8"><p className="mb-4 text-xs font-semibold uppercase tracking-[0.24em] text-[#687858]">Culinary Blog</p><h1 className="font-serif text-4xl leading-tight text-[#28351f] sm:text-5xl">Đăng ký tài khoản</h1><p className="mt-4 text-sm leading-6 text-[#687060]">Tham gia cộng đồng yêu bếp và lưu lại những công thức bạn yêu thích.</p></div>
            {error && <AuthNotice kind="error" className="mb-5 rounded-md border border-[#e7b9a8] bg-[#fff4ef] px-4 py-3 text-sm leading-5 text-[#9a4f3c]" onDismiss={() => setError("")}>{error}</AuthNotice>}
            {success && <AuthNotice kind="success" className="mb-5 rounded-md border border-[#b9cfad] bg-[#f1f8ed] px-4 py-3 text-sm leading-5 text-[#49613b]" onDismiss={() => setSuccess("")}>{success}</AuthNotice>}
            <form className="space-y-4" onSubmit={handleSubmit}>
              <div><label htmlFor="fullName" className="mb-1.5 block text-sm font-semibold text-[#35412c]">Họ và tên</label><input required autoComplete="name" id="fullName" value={form.fullName} onChange={(e) => updateField("fullName", e.target.value)} disabled={loading} placeholder="Ví dụ: Nguyễn Minh Anh" className={inputClass} /></div>
              <div><label htmlFor="registerEmail" className="mb-1.5 block text-sm font-semibold text-[#35412c]">Email</label><input required autoComplete="email" id="registerEmail" type="email" value={form.email} onChange={(e) => updateField("email", e.target.value)} disabled={loading} placeholder="ban@example.com" className={inputClass} /></div>
              <div><label htmlFor="userName" className="mb-1.5 block text-sm font-semibold text-[#35412c]">Tên người dùng</label><input required autoComplete="username" id="userName" value={form.userName} onChange={(e) => updateField("userName", e.target.value)} disabled={loading} placeholder="Ví dụ: MinhAnh01" className={inputClass} /></div>
              <div><label htmlFor="registerPassword" className="mb-1.5 block text-sm font-semibold text-[#35412c]">Mật khẩu</label><input required autoComplete="new-password" id="registerPassword" type="password" value={form.password} onChange={(e) => updateField("password", e.target.value)} disabled={loading} placeholder="Ít nhất 8 ký tự" className={inputClass} /><p className="mt-1.5 text-xs text-[#777e70]">Gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt.</p></div>
              <div><label htmlFor="confirmPassword" className="mb-1.5 block text-sm font-semibold text-[#35412c]">Xác nhận mật khẩu</label><input required autoComplete="new-password" id="confirmPassword" type="password" value={form.confirmPassword} onChange={(e) => updateField("confirmPassword", e.target.value)} disabled={loading} placeholder="Nhập lại mật khẩu" className={inputClass} /></div>
              <button type="submit" disabled={loading} className="mt-2 h-12 w-full rounded-md bg-[#526645] px-5 text-sm font-semibold uppercase tracking-[0.12em] text-white hover:bg-[#3f5135] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#9cad8b] focus-visible:ring-offset-2">{loading ? "Đang tạo tài khoản..." : "Tạo tài khoản"}</button>
            </form>
            <p className="mt-7 text-center text-sm text-[#687060]">Đã có tài khoản? <Link href="/login" className="font-semibold text-[#526645] underline underline-offset-4 hover:text-[#28351f]">Đăng nhập</Link></p>
          </div>
        </div>
      </div>
    </section>
  );
}
