"use client";

import { FormEvent, KeyboardEvent, useCallback, useEffect, useRef, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { getSession, subscribeSession } from "@/services/auth-session";
import { getRecipeSuggestions, type RecipeSuggestion } from "@/services/search-service";
import { clearSearchHistory, deleteSearchHistory, getSearchHistory, saveSearchHistory, type SearchHistoryItem } from "@/services/search-history-service";

const DEBOUNCE_MS = 400;
export default function SearchBar({ compact = false }: { compact?: boolean }) {
  const pathname = usePathname(), searchParams = useSearchParams();
  const currentQuery = pathname === "/recipes" ? searchParams.get("q") ?? "" : "";
  return <SearchBarContent key={currentQuery} compact={compact} currentQuery={currentQuery} />;
}

function SearchBarContent({ compact, currentQuery }: { compact: boolean; currentQuery: string }) {
  const router = useRouter();
  const [value, setValue] = useState(currentQuery), [items, setItems] = useState<RecipeSuggestion[]>([]), [history, setHistory] = useState<SearchHistoryItem[]>([]);
  const [open, setOpen] = useState(false), [loading, setLoading] = useState(false), [error, setError] = useState(false), [active, setActive] = useState(-1);
  const historySession = useRef(getSession()?.sessionId ?? null), historyRequest = useRef(0);
  const resetHistoryForSession = useCallback(() => {
    const sessionId = getSession()?.sessionId ?? null;
    if (historySession.current !== sessionId) {
      historySession.current = sessionId;
      historyRequest.current++;
      setHistory([]);
      setActive(-1);
    }
  }, []);
  useEffect(() => subscribeSession(resetHistoryForSession), [resetHistoryForSession]);
  useEffect(() => { const q = value.trim(); if (q.length < 2) return;
    const controller = new AbortController(); const timer = window.setTimeout(() => { setLoading(true); getRecipeSuggestions(q, controller.signal).then(x => { if (!controller.signal.aborted) setItems(x.items); }).catch(() => { if (!controller.signal.aborted) setError(true); }).finally(() => { if (!controller.signal.aborted) setLoading(false); }); }, DEBOUNCE_MS);
    return () => { window.clearTimeout(timer); controller.abort(); }; }, [value]);
  function changeValue(nextValue: string) { setValue(nextValue); setOpen(true); setActive(-1); setError(false); if (nextValue.trim().length < 2) { setItems([]); setLoading(false); } }
  function navigate(q: string) { q = q.trim(); if (q.length < 2) return; if (getSession()) void saveSearchHistory(q).catch(() => undefined); setOpen(false); router.push(`/recipes?q=${encodeURIComponent(q)}`); }
  function submit(e: FormEvent<HTMLFormElement>) { e.preventDefault(); navigate(active >= 0 ? shown[active]?.text ?? value : value); }
  function keyDown(e: KeyboardEvent<HTMLInputElement>) { if (e.key === "Escape") { setOpen(false); return; } if (!open || !shown.length) return; if (e.key === "ArrowDown" || e.key === "ArrowUp") { e.preventDefault(); setActive(i => (i + (e.key === "ArrowDown" ? 1 : shown.length - 1)) % shown.length); } if (e.key === "Enter" && active >= 0) { e.preventDefault(); navigate(shown[active].text); } }
  function loadHistory() { const sessionId = getSession()?.sessionId; if (!sessionId) { setHistory([]); return; } const version = ++historyRequest.current; void getSearchHistory().then(result => { if (historyRequest.current === version && getSession()?.sessionId === sessionId) setHistory(result); }).catch(() => { if (historyRequest.current === version && getSession()?.sessionId === sessionId) setHistory([]); }); }
  const shown = value.trim().length >= 2 ? items : history.map(x => ({ ...x, text: x.query, type: "history" as const, slug: null }));
  return <div className={`relative w-full ${compact ? "max-w-xs" : "mx-auto mb-8 max-w-2xl"}`}><form className="flex w-full overflow-hidden rounded-full border border-[#d8d9cb] bg-white shadow-sm" role="search" onSubmit={submit}><label className="sr-only" htmlFor="recipe-search">Tìm công thức</label><input className={`min-w-0 flex-1 bg-transparent text-[#262b25] outline-none ${compact ? "px-3 py-2 text-sm" : "px-4 py-3"}`} id="recipe-search" value={value} onFocus={() => { setOpen(true); loadHistory(); }} onChange={e => changeValue(e.target.value)} onKeyDown={keyDown} placeholder="Tìm món ăn..." aria-autocomplete="list" aria-controls="recipe-suggestions" aria-expanded={open} aria-activedescendant={active >= 0 ? `recipe-suggestion-${active}` : undefined}/><button className={`bg-[#254b3b] font-semibold text-white hover:bg-[#18382b] ${compact ? "px-3 py-2 text-sm" : "px-5 py-3"}`} type="submit" aria-label="Tìm kiếm">⌕</button></form>
    {open && <div id="recipe-suggestions" role="listbox" className="absolute z-20 mt-2 w-full rounded-xl border border-[#d8d9cb] bg-white shadow-lg">{loading && <p className="p-3 text-sm" role="status">Đang tìm gợi ý…</p>}{error && <p className="p-3 text-sm text-red-700" role="alert">Chưa thể tải gợi ý.</p>}{!loading && !error && !shown.length && <p className="p-3 text-sm">Không có gợi ý.</p>}{shown.map((item, i) => <div key={`${item.type}:${item.text}:${i}`} id={`recipe-suggestion-${i}`} role="option" aria-selected={active === i} className={`flex px-3 py-2 ${active === i ? "bg-[#edf5ef]" : ""}`}><button type="button" className="flex-1 text-left" onMouseDown={e => e.preventDefault()} onClick={() => navigate(item.text)}>{item.text}</button>{"id" in item && <button type="button" aria-label={`Xóa ${item.text}`} onClick={() => void deleteSearchHistory(item.id).then(() => setHistory(x => x.filter(y => y.id !== item.id)))}>×</button>}</div>)}{!value.trim() && history.length > 0 && <button type="button" className="w-full border-t px-3 py-2 text-left text-sm" onClick={() => void clearSearchHistory().then(() => setHistory([]))}>Xóa toàn bộ lịch sử</button>}</div>}</div>;
}
