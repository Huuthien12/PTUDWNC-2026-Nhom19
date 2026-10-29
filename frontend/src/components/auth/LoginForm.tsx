"use client";

import { FormEvent, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { ApiError } from "@/services/api-client";
import { login, loginWithGoogle } from "@/services/auth-service";
import { loginDestination } from "@/services/auth-navigation";
import { beginGoogleLogin, consumeGoogleCallback } from "@/services/google-oauth";
import AuthNotice from "@/components/auth/AuthNotice";

export default function LoginForm({ next, reason }: { next?: string; reason?: string }) {
  const router = useRouter();
  const searchParams = useSearchParams();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState("");
  const completingGoogle = useRef(false);

  useEffect(() => {
    if (!searchParams.get("code") && !searchParams.get("error") || completingGoogle.current) return;
    completingGoogle.current = true;
    setError("");
    setLoading(true);
    void (async () => {
      try {
        await loginWithGoogle(consumeGoogleCallback(searchParams));
        setSuccess("Đăng nhập Google thành công. Đang chuyển hướng...");
        router.replace(loginDestination(next));
      } catch (googleError) {
        setError(googleError instanceof Error ? googleError.message : "Không thể đăng nhập bằng Google.");
      } finally {
        setLoading(false);
      }
    })();
  }, [next, router, searchParams]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    setError("");
    setSuccess("");

    if (!email.trim() || !password) {
      setError("\u0056\u0075\u0069 \u006c\u00f2\u006e\u0067 \u006e\u0068\u1ead\u0070 \u0065\u006d\u0061\u0069\u006c \u0076\u00e0 \u006d\u1ead\u0074 \u006b\u0068\u1ea9\u0075.");
      return;
    }

    try {
      setLoading(true);

      await login({
        email: email.trim(),
        password,
      });

      router.replace(loginDestination(next));
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.status === 0) {
          setError(err.message);
        } else if (err.status === 401) {
          setError("\u0045\u006d\u0061\u0069\u006c \u0068\u006f\u1eb7\u0063 \u006d\u1ead\u0074 \u006b\u0068\u1ea9\u0075 \u006b\u0068\u00f4\u006e\u0067 \u0063\u0068\u00ed\u006e\u0068 \u0078\u00e1\u0063.");
        } else if (err.status === 423) {
          setError("Tài khoản tạm khóa sau 5 lần đăng nhập sai. Vui lòng thử lại khi hết thời gian khóa.");
        } else if (err.status === 400) {
          setError("\u0054\u0068\u00f4\u006e\u0067 \u0074\u0069\u006e \u0111\u0103\u006e\u0067 \u006e\u0068\u1ead\u0070 \u006b\u0068\u00f4\u006e\u0067 \u0068\u1ee3\u0070 \u006c\u1ec7.");
        } else {
          setError("\u0043\u00f3 \u006c\u1ed7\u0069 \u0078\u1ea3\u0079 \u0072\u0061. \u0056\u0075\u0069 \u006c\u00f2\u006e\u0067 \u0074\u0068\u1eed \u006c\u1ea1\u0069.");
        }
      } else {
        setError("\u004b\u0068\u00f4\u006e\u0067 \u0074\u0068\u1ec3 \u006b\u1ebf\u0074 \u006e\u1ed1\u0069 \u0111\u1ebf\u006e \u006d\u00e1\u0079 \u0063\u0068\u1ee7.");
      }
    } finally {
      setLoading(false);
    }
  }

  async function handleGoogleLogin() {
    if (loading) return;
    setError("");
    setSuccess("");
    setLoading(true);
    try {
      await beginGoogleLogin();
    } catch (googleError) {
      setError(googleError instanceof Error ? googleError.message : "Không thể bắt đầu đăng nhập Google.");
      setLoading(false);
    }
  }

  return (
    <section className="flex flex-1 items-center bg-[#fffdf7] px-4 py-10 sm:px-8 lg:px-12">
      <div className="mx-auto grid w-full max-w-6xl overflow-hidden rounded-2xl border border-[#e8e3d1] bg-white shadow-[0_18px_55px_rgba(66,74,48,0.09)] md:grid-cols-[1.05fr_0.95fr]">
        <aside className="relative flex min-h-64 flex-col justify-between overflow-hidden bg-[#dce4d3] p-7 sm:p-10 md:min-h-[620px] md:p-14">
          <div aria-hidden="true" className="absolute -right-20 -top-20 h-72 w-72 rounded-full bg-[#254b3b]/[0.06]" />
          <div aria-hidden="true" className="absolute -bottom-20 -left-16 h-64 w-64 rounded-full bg-[#faf8f2]/80" />
          <Link href="/" className="cb-brand relative z-10 w-fit whitespace-nowrap tracking-normal text-[1.8rem] leading-tight" style={{ fontFamily: "var(--font-header)" }} aria-label="Culinary Blog — Trang chủ">Culinary <span>Blog</span><span className="text-gold" aria-hidden="true">.</span></Link>
          <div className="relative z-10 flex flex-col items-start md:mt-8">
            <div className="mb-7 flex h-40 w-40 items-center justify-center self-center rounded-full bg-[#91aa82] shadow-[0_12px_30px_rgba(55,83,47,0.16)] sm:h-48 sm:w-48 md:self-start">
              <svg aria-hidden="true" viewBox="0 0 180 180" className="h-32 w-32 sm:h-40 sm:w-40">
                <ellipse cx="91" cy="101" rx="55" ry="43" fill="#f8e77e" stroke="#d6ad2a" strokeWidth="4" transform="rotate(-22 91 101)" />
                <path d="M94 59c10-20 28-25 43-20-7 16-21 25-43 20Z" fill="#4d7b4d" />
                <path d="M91 67c8-13 19-19 34-23" fill="none" stroke="#315d3e" strokeWidth="4" strokeLinecap="round" />
                <path d="M56 91c18-14 42-20 70-18M52 106c24-13 50-16 77-11M62 122c20-9 39-11 59-8" fill="none" stroke="#fff7c5" strokeWidth="4" strokeLinecap="round" opacity=".85" />
              </svg>
            </div>
            <p className="mb-3 text-xs font-bold uppercase tracking-[0.2em] text-[#648048]">Bếp nhà, chuyện hay</p>
            <h2 className="max-w-md text-3xl font-bold leading-tight tracking-tight text-[#254b3b] sm:text-4xl">Món ngon mỗi ngày, cảm hứng luôn mới.</h2>
            <p className="mt-4 max-w-md text-sm leading-7 text-[#58634e]">Lưu lại công thức yêu thích và chia sẻ câu chuyện ẩm thực của bạn cùng cộng đồng.</p>
          </div>
          <p className="relative z-10 mt-8 text-xs font-semibold tracking-wide text-[#648048]">Nấu ngon · Sống vui · Kể chuyện</p>
        </aside>

        <div className="flex items-center px-6 py-10 sm:px-10 md:px-12 lg:px-16">
          <div className="mx-auto w-full max-w-md">
              <p className="mb-3 text-xs font-bold uppercase tracking-[0.18em] text-[#648048]">Chào mừng trở lại</p>
              <h1 className="text-3xl font-bold tracking-tight text-[#254b3b] sm:text-4xl">Đăng nhập</h1>
              <p className="mt-3 mb-7 text-sm leading-6 text-[#687060]">Đăng nhập để lưu công thức và tiếp tục khám phá những món ngon.</p>

              {reason === "session-expired" && <p role="status" className="mb-4 rounded-md border border-[#d8d9cb] bg-[#f0ecdf] px-4 py-3 text-sm text-[#254b3b]">Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.</p>}
              {reason === "logout-unconfirmed" && <p role="status" className="mb-4 rounded-md border border-[#d8d9cb] bg-[#f0ecdf] px-4 py-3 text-sm text-[#254b3b]">Đã xóa phiên trên thiết bị này, nhưng chưa xác nhận được việc thu hồi phiên trên máy chủ.</p>}
              {error && <AuthNotice kind="error" className="mb-4 rounded-md border border-[#e7c3b8] bg-[#fcf0ec] px-4 py-3 text-sm text-[#922f2b]" onDismiss={() => setError("")}>{error}</AuthNotice>}
              {success && <AuthNotice kind="success" className="mb-4 rounded-md border border-[#b9cfad] bg-[#edf4e8] px-4 py-3 text-sm text-[#254b3b]" onDismiss={() => setSuccess("")}>{success}</AuthNotice>}

              <form onSubmit={handleSubmit} className="space-y-5">
                <div className="cb-field !mt-0">
                  <label htmlFor="email">Email</label>
                  <input id="email" name="email" type="email" autoComplete="email" required value={email} onChange={(event) => setEmail(event.target.value)} placeholder="ban@example.com" disabled={loading} className="cb-input" />
                </div>
                <div className="cb-field !mt-0">
                  <div className="mb-2 flex items-center justify-between gap-3">
                    <label htmlFor="password" className="!mb-0">Mật khẩu</label>
                    <Link href="/forgot-password" className="cb-text-link text-xs">Quên mật khẩu?</Link>
                  </div>
                  <input id="password" name="password" type="password" autoComplete="current-password" required value={password} onChange={(event) => setPassword(event.target.value)} placeholder="Nhập mật khẩu" disabled={loading} className="cb-input" />
                </div>
                <button type="submit" disabled={loading} className="cb-button w-full">
                  {loading ? "Đang đăng nhập..." : "Đăng nhập"}
                </button>
              </form>

              <div className="my-5 flex items-center gap-3 text-xs text-[#61695c]"><span className="h-px flex-1 bg-[#d8d9cb]" /><span>Hoặc</span><span className="h-px flex-1 bg-[#d8d9cb]" /></div>
              <button type="button" disabled={loading} onClick={() => void handleGoogleLogin()} className="cb-button cb-button-secondary w-full">
                <svg aria-hidden="true" viewBox="0 0 24 24" className="h-5 w-5"><path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 0 1-2.2 3.32v2.77h3.57c2.08-1.92 3.27-4.74 3.27-8.1Z"/><path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.65l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.15v2.84A11 11 0 0 0 12 23Z"/><path fill="#FBBC05" d="M5.84 14.11a6.6 6.6 0 0 1 0-4.22V7.05H2.15a11 11 0 0 0 0 9.9l3.69-2.84Z"/><path fill="#EA4335" d="M12 4.36c1.62 0 3.06.56 4.21 1.64l3.15-3.15A10.55 10.55 0 0 0 12 0 11 11 0 0 0 2.15 7.05l3.69 2.84C6.71 7.29 9.14 4.36 12 4.36Z"/></svg>
                {loading ? "Đang xác thực..." : "Tiếp tục với Google"}
              </button>
              <p className="mt-6 text-center text-sm text-[#687060]">Chưa có tài khoản? <Link href="/register" className="font-semibold text-[#4f713f] underline decoration-[#c9ad4a] underline-offset-4 hover:text-[#254b3b]">Đăng ký ngay</Link></p>
          </div>
        </div>
      </div>
    </section>
  );
}
