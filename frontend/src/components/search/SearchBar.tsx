"use client";

import { FormEvent, useEffect, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";

const DEBOUNCE_MS = 400;

export default function SearchBar({ compact = false }: { compact?: boolean }) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const currentQuery = pathname === "/recipes" ? searchParams.get("q") ?? "" : "";
  const [draft, setDraft] = useState({ baseQuery: currentQuery, text: currentQuery });
  const value = draft.baseQuery === currentQuery ? draft.text : currentQuery;

  useEffect(() => {
    const trimmed = value.trim();
    if (pathname !== "/recipes" || trimmed === currentQuery.trim()) return;
    if (trimmed.length === 0 && currentQuery.trim().length > 0) {
      const timer = window.setTimeout(() => { router.replace("/recipes"); }, DEBOUNCE_MS);
      return () => window.clearTimeout(timer);
    }
    if (trimmed.length < 2) return;
    const timer = window.setTimeout(() => {
      router.replace(`/recipes?q=${encodeURIComponent(trimmed)}`);
    }, DEBOUNCE_MS);
    return () => window.clearTimeout(timer);
  }, [currentQuery, pathname, router, value]);

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const query = value.trim();
    if (query.length >= 2) router.push(`/recipes?q=${encodeURIComponent(query)}`);
    else if (pathname === "/recipes" && query.length === 0 && currentQuery.trim().length > 0) router.push("/recipes");
  }

  return (
    <form className={`flex w-full overflow-hidden rounded-full border border-[#d8d9cb] bg-white shadow-sm ${compact ? "max-w-xs" : "mx-auto mb-8 max-w-2xl"}`} role="search" onSubmit={submit}>
      <label className="sr-only" htmlFor="recipe-search">Tìm công thức</label>
      <input className={`min-w-0 flex-1 bg-transparent text-[#262b25] outline-none ${compact ? "px-3 py-2 text-sm" : "px-4 py-3"}`} id="recipe-search" value={value} onChange={(event) => setDraft({ baseQuery: currentQuery, text: event.target.value })}
        placeholder="Tìm món ăn..." aria-label="Tìm món ăn" />
      <button className={`bg-[#254b3b] font-semibold text-white hover:bg-[#18382b] ${compact ? "px-3 py-2 text-sm" : "px-5 py-3"}`} type="submit" aria-label="Tìm kiếm"><span aria-hidden="true">⌕</span><span className={compact ? "sr-only" : ""}>Tìm</span></button>
    </form>
  );
}
