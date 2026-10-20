"use client";

import { FormEvent, useEffect, useRef, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
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

  const inputClass = "h-12 w-full rounded-md border border-[#cbd1c2] bg-white px-3 text-[15px] text-[#28351f] outline-none transition placeholder:text-[#92998b] focus:border-[#526645] focus:ring-2 focus:ring-[#526645]/20 disabled:opacity-60";
  return (
    <section className="relative flex min-h-[calc(100vh-8rem)] items-center overflow-hidden bg-[#f7f5ef] px-4 py-10 sm:px-8 lg:px-12">
      <div className="absolute inset-y-0 right-0 hidden w-[42%] bg-[#dfe8d5] lg:block" />
      <div className="relative z-10 mx-auto grid w-full max-w-6xl overflow-hidden border border-[#d8d8ca] bg-white shadow-[0_18px_60px_rgba(75,82,55,0.12)] lg:grid-cols-[1.1fr_0.9fr]">
        <div className="flex items-center px-6 py-10 sm:px-12 lg:px-16 lg:py-14">
          <div className="mx-auto w-full max-w-md">
        <p className="mb-4 text-xs font-semibold uppercase tracking-[0.24em] text-[#687858]">Culinary Blog</p>
        <h1 className="font-serif text-4xl leading-tight text-[#28351f] sm:text-5xl">Đăng nhập</h1>
        <p className="mb-7 mt-4 text-sm leading-6 text-[#687060]">Chào mừng bạn trở lại căn bếp của mình.</p>

        {reason === "session-expired" && <p role="status" className="mb-4 rounded-md border border-[#d9c68b] bg-[#fff9e8] px-4 py-3 text-sm text-[#695528]">Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.</p>}
        {reason === "logout-unconfirmed" && <p role="status" className="mb-4 rounded-md border border-[#d9c68b] bg-[#fff9e8] px-4 py-3 text-sm text-[#695528]">Đã xóa phiên trên thiết bị này, nhưng chưa xác nhận được việc thu hồi phiên trên máy chủ.</p>}
        {error && (
          <AuthNotice
            kind="error"
            className="mb-4 rounded-md border border-[#e7b9a8] bg-[#fff4ef] px-4 py-3 text-sm text-[#9a4f3c]"
            onDismiss={() => setError("")}
          >
            {error}
          </AuthNotice>
        )}
        {success && (
          <AuthNotice
            kind="success"
            className="mb-4 rounded-md border border-[#b9cfad] bg-[#f1f8ed] px-4 py-3 text-sm text-[#49613b]"
            onDismiss={() => setSuccess("")}
          >
            {success}
          </AuthNotice>
        )}

        <form onSubmit={handleSubmit} className="space-y-5">
          <div>
            <label
              htmlFor="email"
              className="mb-1 block text-sm font-medium"
            >
              Email
            </label>

            <input
              id="email"
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              placeholder="Nh\u1eadp email"
              disabled={loading}
              autoComplete="email"
              required
              className={inputClass}
            />
          </div>

          <div>
            <label
              htmlFor="password"
              className="mb-1 block text-sm font-medium"
            >
              {"M\u1eadt kh\u1ea9u"}
            </label>

            <input
              id="password"
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              placeholder="Nh\u1eadp m\u1eadt kh\u1ea9u"
              disabled={loading}
              autoComplete="current-password"
              required
              className={inputClass}
            />
          </div>

          <button
            type="submit"
            disabled={loading}
            className="h-12 w-full rounded-md bg-[#526645] px-4 font-semibold uppercase tracking-[0.1em] text-white hover:bg-[#3f5135] focus-visible:ring-2 focus-visible:ring-[#9cad8b] focus-visible:ring-offset-2"
          >
            {loading
              ? "\u0110ang \u0111\u0103ng nh\u1eadp..."
              : "\u0110\u0103ng nh\u1eadp"}
          </button>
        </form>
        <button type="button" disabled={loading} onClick={() => void handleGoogleLogin()}
          className="mt-3 h-12 w-full rounded-md border border-[#cbd1c2] px-4 font-medium text-[#35412c] hover:bg-[#f7f5ef] focus-visible:ring-2 focus-visible:ring-[#9cad8b]">
          {loading ? "Đang xác thực..." : "Đăng nhập với Google"}
        </button>
        <p className="mt-7 text-center text-sm text-[#687060]">Chưa có tài khoản? <Link href="/register" className="font-semibold text-[#526645] underline underline-offset-4 hover:text-[#28351f]">Đăng ký</Link></p>
          </div>
        </div>
        <div className="relative hidden min-h-[580px] bg-cover bg-center lg:block" style={{ backgroundImage: "linear-gradient(180deg, rgba(40,53,27,0.04), rgba(40,53,27,0.55)), url('https://images.unsplash.com/photo-1490645935967-10de6ba17061?auto=format&fit=crop&w=1000&q=85')" }}>
          <div className="absolute inset-x-8 bottom-9 text-white"><p className="mb-3 text-xs font-semibold uppercase tracking-[0.28em] text-[#e5efcf]">Bếp nhỏ, cảm hứng lớn</p><h2 className="max-w-sm font-serif text-4xl leading-tight">Món ngon bắt đầu<br />từ câu chuyện của bạn.</h2></div>
        </div>
      </div>
    </section>
  );
}
