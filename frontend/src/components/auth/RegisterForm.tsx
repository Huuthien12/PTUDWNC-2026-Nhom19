"use client";

import { FormEvent, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5062";

export default function RegisterForm() {
  const router = useRouter();
  const [form, setForm] = useState({
    fullName: "",
    email: "",
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

    if (!form.fullName.trim() || !form.email.trim() || !form.password || !form.confirmPassword) {
      setError("Vui lòng điền đầy đủ thông tin.");
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
      const response = await fetch(`${API_URL}/api/v1/auth/register`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          ...form,
          fullName: form.fullName.trim(),
          email: form.email.trim(),
        }),
      });

      const result = (await response.json()) as {
        message?: string;
        detail?: string;
        errors?: string[] | Record<string, string[]>;
      };

      if (!response.ok) {
        const serverErrors = Array.isArray(result.errors)
          ? result.errors
          : Object.values(result.errors ?? {}).flat();
        throw new Error([result.detail ?? result.message, ...serverErrors].filter(Boolean).join(" "));
      }

      setSuccess(result.message || "Đăng ký tài khoản thành công.");
      setTimeout(() => router.push("/login"), 1200);
    } catch (err) {
      setError(err instanceof Error && err.message ? err.message : "Không thể kết nối đến máy chủ.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className="relative flex min-h-[calc(100vh-8rem)] items-center overflow-hidden bg-[#f7f5ef] px-4 py-10 sm:px-8 lg:px-12">
      <div className="absolute inset-y-0 left-0 hidden w-[42%] bg-[#dfe8d5] lg:block" />
      <div className="absolute -left-24 top-12 hidden h-72 w-72 rounded-full border-[28px] border-[#c8d6b9] lg:block" />
      <div className="absolute bottom-[-7rem] left-[28%] hidden h-64 w-64 rounded-full bg-[#efc7a7] opacity-75 lg:block" />

      <div className="relative z-10 mx-auto grid w-full max-w-6xl overflow-hidden border border-[#d8d8ca] bg-white shadow-[0_18px_60px_rgba(75,82,55,0.12)] lg:grid-cols-[0.9fr_1.1fr]">
        <div
          className="relative hidden min-h-[620px] bg-cover bg-center lg:block"
          style={{
            backgroundImage:
              "linear-gradient(180deg, rgba(40,53,27,0.04), rgba(40,53,27,0.4)), url('https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=1000&q=85')",
          }}
        >
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
              <p className="mb-4 text-xs font-semibold uppercase tracking-[0.24em] text-[#687858]">
                Culinary Blog
              </p>
              <h1 className="font-serif text-4xl leading-tight text-[#28351f] sm:text-5xl">
                Đăng ký tài khoản
              </h1>
              <p className="mt-4 max-w-sm text-sm leading-6 text-[#687060]">
                Tham gia cộng đồng yêu bếp và lưu lại những công thức bạn yêu thích.
              </p>
            </div>

            {error && (
              <div className="mb-5 border border-[#e7b9a8] bg-[#fff4ef] px-4 py-3 text-sm leading-5 text-[#9a4f3c]" role="alert">
                {error}
              </div>
            )}
            {success && (
              <div className="mb-5 border border-[#b9cfad] bg-[#f1f8ed] px-4 py-3 text-sm leading-5 text-[#49613b]" role="status">
                {success} Đang chuyển đến trang đăng nhập...
              </div>
            )}

            <form className="space-y-5" onSubmit={handleSubmit}>
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
                  className="h-12 w-full border-b border-[#bfc7b5] bg-transparent px-0 text-[15px] text-[#28351f] outline-none transition placeholder:text-[#a5aa9d] focus:border-[#687858]"
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
                  className="h-12 w-full border-b border-[#bfc7b5] bg-transparent px-0 text-[15px] text-[#28351f] outline-none transition placeholder:text-[#a5aa9d] focus:border-[#687858]"
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
                  className="h-12 w-full border-b border-[#bfc7b5] bg-transparent px-0 text-[15px] text-[#28351f] outline-none transition placeholder:text-[#a5aa9d] focus:border-[#687858]"
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
                  className="h-12 w-full border-b border-[#bfc7b5] bg-transparent px-0 text-[15px] text-[#28351f] outline-none transition placeholder:text-[#a5aa9d] focus:border-[#687858]"
                />
              </div>

              <button
                type="submit"
                disabled={loading}
                className="mt-3 h-13 w-full bg-[#526645] px-5 text-sm font-semibold uppercase tracking-[0.12em] text-white transition hover:bg-[#3f5135] focus:outline-none focus:ring-2 focus:ring-[#9cad8b] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {loading ? "Đang tạo tài khoản..." : "Tạo tài khoản"}
              </button>
            </form>

            <p className="mt-7 text-center text-sm text-[#687060]">
              Đã có tài khoản?{" "}
              <Link href="/login" className="font-semibold text-[#526645] underline underline-offset-4 hover:text-[#28351f]">
                Đăng nhập
              </Link>
            </p>
          </div>
        </div>
      </div>
    </section>
  );
}