"use client";

import { FormEvent, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { register } from "@/services/auth-service";
import AuthNotice from "@/components/auth/AuthNotice";

export default function RegisterForm() {
  const router = useRouter();
  const [form, setForm] = useState({
    fullName: "",
    email: "",
    userName: "",
    password: "",
    confirmPassword: "",
  });
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState("");

  function updateField(field: keyof typeof form, value: string) {
    setForm((current) => ({ ...current, [field]: value }));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    setSuccess("");

    if (!form.fullName.trim() || !form.email.trim() || !form.userName.trim() || !form.password || !form.confirmPassword) {
      setError("Vui lòng điền đầy đủ thông tin.");
      return;
    }

    if (!/^[a-zA-Z0-9]+$/.test(form.userName)) {
      setError("Tên người dùng chỉ gồm chữ cái và chữ số.");
      return;
    }

    if (form.password.length < 8 || !/[A-Z]/.test(form.password) || !/[a-z]/.test(form.password) || !/[0-9]/.test(form.password) || !/[\W_]/.test(form.password)) {
      setError("Mật khẩu cần ít nhất 8 ký tự, gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt.");
      return;
    }

    if (form.password !== form.confirmPassword) {
      setError("Xác nhận mật khẩu không khớp.");
      return;
    }

    try {
      setLoading(true);
      await register({
        fullName: form.fullName.trim(),
        email: form.email.trim(),
        userName: form.userName.trim(),
        password: form.password,
      });
      setSuccess("Đăng ký thành công. Đang chuyển đến trang chủ...");
      setTimeout(() => router.push("/"), 1200);
    } catch (err) {
      setError(err instanceof Error && err.message ? err.message : "Không thể kết nối đến máy chủ.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className="relative flex min-h-[calc(100vh-8rem)] items-center overflow-hidden bg-[#faf8f2] px-4 py-10 sm:px-8 lg:px-12">
      <div className="absolute inset-y-0 left-0 hidden w-[42%] bg-[#dce4d3] lg:block" />
      <div className="absolute -left-24 top-12 hidden h-72 w-72 rounded-full border-[28px] border-[#d7b354]/30 lg:block" />
      <div className="absolute bottom-[-7rem] left-[28%] hidden h-64 w-64 rounded-full bg-[#d7b354]/20 lg:block" />

      <div className="relative z-10 mx-auto grid w-full max-w-6xl overflow-hidden rounded-2xl border border-[#e8e3d1] bg-white shadow-[0_18px_55px_rgba(66,74,48,0.09)] lg:grid-cols-[0.9fr_1.1fr]">
        <div
          className="relative hidden min-h-[620px] bg-cover bg-center lg:block"
          style={{
            backgroundImage:
              "linear-gradient(180deg, rgba(40,53,27,0.04), rgba(40,53,27,0.4)), url('https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=1000&q=85')",
          }}
        >
          <Link href="/" className="cb-brand absolute left-8 top-8 z-10 w-fit whitespace-nowrap rounded-lg bg-[#faf8f2]/95 px-3 py-2 tracking-normal text-[1.8rem] leading-tight shadow-sm" style={{ fontFamily: "var(--font-header)" }} aria-label="Culinary Blog — Trang chủ">Culinary <span>Blog</span><span className="text-gold" aria-hidden="true">.</span></Link>
          <div className="absolute inset-x-8 bottom-9 text-white">
            <p className="mb-3 text-xs font-semibold uppercase tracking-[0.28em] text-[#e5efcf]">
              Bếp nhỏ, cảm hứng lớn
            </p>
            <h2
              className="max-w-sm text-4xl leading-[1.05]"
              style={{
                fontFamily: "Georgia, 'Times New Roman', serif",
                letterSpacing: "0",
                wordSpacing: "normal",
              }}
            >
              <span className="block whitespace-nowrap">Làm một món ngon.</span>
              <span className="block whitespace-nowrap">Kể một câu chuyện.</span>
            </h2>
          </div>
        </div>

        <div className="flex items-center px-6 py-10 sm:px-12 lg:px-16 lg:py-14">
          <div className="w-full max-w-md">
            <div className="mb-9">
              <Link href="/" className="cb-brand mb-4 inline-block whitespace-nowrap tracking-normal text-[1.8rem] leading-tight lg:hidden" style={{ fontFamily: "var(--font-header)" }} aria-label="Culinary Blog — Trang chủ">Culinary <span>Blog</span><span className="text-gold" aria-hidden="true">.</span></Link>
              <h1 className="text-3xl font-bold tracking-tight text-[#254b3b] sm:text-4xl">
                Đăng ký tài khoản
              </h1>
              <p className="mt-4 max-w-sm text-sm leading-6 text-[#687060]">
                Tham gia cộng đồng yêu bếp và lưu lại những công thức bạn yêu thích.
              </p>
            </div>

            {error && (
              <AuthNotice
                kind="error"
                className="mb-5 border border-[#e7b9a8] bg-[#fff4ef] px-4 py-3 text-sm leading-5 text-[#9a4f3c]"
                onDismiss={() => setError("")}
              >
                {error}
              </AuthNotice>
            )}
            {success && (
              <AuthNotice
                kind="success"
                className="mb-5 border border-[#b9cfad] bg-[#f1f8ed] px-4 py-3 text-sm leading-5 text-[#49613b]"
                onDismiss={() => setSuccess("")}
              >
                {success}
              </AuthNotice>
            )}

            <form className="space-y-4" onSubmit={handleSubmit}>
              <div>
                <label htmlFor="fullName" className="mb-2 block text-sm font-semibold text-[#35412c]">
                  Họ và tên
                </label>
                <input
                  id="fullName"
                  name="fullName"
                  type="text"
                  value={form.fullName}
                  onChange={(event) => updateField("fullName", event.target.value)}
                  disabled={loading}
                  placeholder="Ví dụ: Nguyễn Minh Anh"
                  className="h-12 w-full border-b border-[#bfc7b5] bg-transparent px-0 text-[15px] text-[#28351f] outline-none transition placeholder:text-[#a5aa9d] focus:border-[#254b3b]"
                />
              </div>

              <div>
                <label htmlFor="email" className="mb-2 block text-sm font-semibold text-[#35412c]">
                  Email
                </label>
                <input
                  id="email"
                  name="email"
                  type="email"
                  value={form.email}
                  onChange={(event) => updateField("email", event.target.value)}
                  disabled={loading}
                  placeholder="ban@example.com"
                  className="h-12 w-full border-b border-[#bfc7b5] bg-transparent px-0 text-[15px] text-[#28351f] outline-none transition placeholder:text-[#a5aa9d] focus:border-[#254b3b]"
                />
              </div>

              <div>
                <label htmlFor="userName" className="mb-2 block text-sm font-semibold text-[#35412c]">
                  Tên người dùng
                </label>
                <input
                  id="userName"
                  name="userName"
                  type="text"
                  value={form.userName}
                  onChange={(event) => updateField("userName", event.target.value)}
                  disabled={loading}
                  placeholder="Ví dụ: MinhAnh01"
                  className="h-12 w-full border-b border-[#bfc7b5] bg-transparent px-0 text-[15px] text-[#28351f] outline-none transition placeholder:text-[#a5aa9d] focus:border-[#254b3b]"
                />
              </div>

              <div>
                <label htmlFor="password" className="mb-2 block text-sm font-semibold text-[#35412c]">
                  Mật khẩu
                </label>
                <input
                  id="password"
                  name="password"
                  type="password"
                  value={form.password}
                  onChange={(event) => updateField("password", event.target.value)}
                  disabled={loading}
                  placeholder="Tạo mật khẩu của bạn"
                  className="h-12 w-full border-b border-[#bfc7b5] bg-transparent px-0 text-[15px] text-[#28351f] outline-none transition placeholder:text-[#a5aa9d] focus:border-[#254b3b]"
                />
              </div>

              <div>
                <label htmlFor="confirmPassword" className="mb-2 block text-sm font-semibold text-[#35412c]">
                  Xác nhận mật khẩu
                </label>
                <input
                  id="confirmPassword"
                  name="confirmPassword"
                  type="password"
                  value={form.confirmPassword}
                  onChange={(event) => updateField("confirmPassword", event.target.value)}
                  disabled={loading}
                  placeholder="Nhập lại mật khẩu"
                  className="h-12 w-full border-b border-[#bfc7b5] bg-transparent px-0 text-[15px] text-[#28351f] outline-none transition placeholder:text-[#a5aa9d] focus:border-[#254b3b]"
                />
              </div>

              <button
                type="submit"
                disabled={loading}
                className="mt-3 h-13 w-full rounded-full bg-[#254b3b] px-5 text-sm font-semibold text-white transition hover:bg-[#18382b] focus:outline-none focus:ring-2 focus:ring-[#d7b354] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {loading ? "Đang tạo tài khoản..." : "Tạo tài khoản"}
              </button>
            </form>

            <p className="mt-7 text-center text-sm text-[#687060]">
              Đã có tài khoản?{" "}
              <Link href="/login" className="font-semibold text-[#254b3b] underline underline-offset-4 hover:text-[#18382b]">
                Đăng nhập
              </Link>
            </p>
          </div>
        </div>
      </div>
    </section>
  );
}
