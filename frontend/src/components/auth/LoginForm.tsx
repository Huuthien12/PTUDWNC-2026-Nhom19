"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { ApiError } from "@/services/api-client";
import { login } from "@/services/auth-service";

export default function LoginForm() {
  const router = useRouter();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    setError("");

    if (!email.trim() || !password) {
      setError("\u0056\u0075\u0069 \u006c\u00f2\u006e\u0067 \u006e\u0068\u1ead\u0070 \u0065\u006d\u0061\u0069\u006c \u0076\u00e0 \u006d\u1ead\u0074 \u006b\u0068\u1ea9\u0075.");
      return;
    }

    try {
      setLoading(true);

      const response = await login({
        email: email.trim(),
        password,
      });

      localStorage.setItem("accessToken", response.accessToken);
      localStorage.setItem("tokenType", response.tokenType);

      router.push("/");
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.status === 401) {
          setError("\u0045\u006d\u0061\u0069\u006c \u0068\u006f\u1eb7\u0063 \u006d\u1ead\u0074 \u006b\u0068\u1ea9\u0075 \u006b\u0068\u00f4\u006e\u0067 \u0063\u0068\u00ed\u006e\u0068 \u0078\u00e1\u0063.");
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

  return (
    <div className="mx-auto flex min-h-[70vh] max-w-md items-center justify-center px-4">
      <div className="w-full rounded-xl border bg-white p-6 shadow-sm">
        <h1 className="mb-2 text-2xl font-bold">
          {"\u0110\u0103ng nh\u1eadp"}
        </h1>

        <p className="mb-6 text-sm text-gray-500">
          {"\u0110\u0103ng nh\u1eadp v\u00e0o t\u00e0i kho\u1ea3n Culinary Blog"}
        </p>

        {error && (
          <div className="mb-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
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
              className="w-full rounded-lg border px-3 py-2 outline-none focus:ring-2"
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
              className="w-full rounded-lg border px-3 py-2 outline-none focus:ring-2"
            />
          </div>

          <button
            type="submit"
            disabled={loading}
            className="w-full rounded-lg bg-black px-4 py-2 font-medium text-white disabled:cursor-not-allowed disabled:opacity-50"
          >
            {loading
              ? "\u0110ang \u0111\u0103ng nh\u1eadp..."
              : "\u0110\u0103ng nh\u1eadp"}
          </button>
        </form>
      </div>
    </div>
  );
}
