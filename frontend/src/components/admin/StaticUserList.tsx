"use client";

import { useCallback, useEffect, useState } from "react";
import { getAdminUsers } from "@/services/admin-user-service";
import type { AdminUser, AdminUserPage } from "@/types/admin-user";

const pageSize = 12;

export default function StaticUserList({ users: presentationUsers }: { users?: readonly AdminUser[] }) {
  const [result, setResult] = useState<AdminUserPage | null>(() => presentationUsers ? {
    items: [...presentationUsers], totalCount: presentationUsers.length, page: 1, pageSize, totalPages: 1,
  } : null);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [role, setRole] = useState("");
  const [state, setState] = useState<"loading" | "ready" | "error">(presentationUsers ? "ready" : "loading");
  const load = useCallback(async (targetPage = page) => {
    setState("loading");
    try { setResult(await getAdminUsers(targetPage, pageSize, search, role)); setPage(targetPage); setState("ready"); }
    catch { setState("error"); }
  }, [page, role, search]);
  useEffect(() => {
    if (presentationUsers) return;
    void getAdminUsers(1, pageSize, "", role).then((next) => {
      setResult(next); setPage(1); setState("ready");
    }).catch(() => setState("error"));
  }, [presentationUsers, role]);
  if (state === "loading" && !result) return <section role="status">Đang tải người dùng...</section>;
  if (state === "error") return <section><h1 className="cb-section-title">Quản lý người dùng</h1><p role="alert">Không thể tải người dùng. <button className="underline" onClick={() => void load()}>Thử lại</button></p></section>;
  const users = result?.items ?? [];
  return <section className="space-y-6"><header><h1 className="cb-section-title">Quản lý người dùng</h1></header>
    <form onSubmit={(event) => { event.preventDefault(); void load(1); }} className="grid gap-3 sm:grid-cols-[1fr_auto_auto]">
      <input aria-label="Tìm kiếm người dùng" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Tên, email hoặc tên người dùng" className="w-full rounded-lg border p-3" />
      <select aria-label="Lọc theo vai trò" value={role} onChange={(event) => setRole(event.target.value)} className="rounded-lg border p-3"><option value="">Tất cả vai trò</option><option value="Admin">Admin</option><option value="Author">Author</option></select>
      <button className="cb-button" type="submit">Tìm</button>
    </form>
    {state === "loading" && <p role="status">Đang cập nhật danh sách...</p>}
    {!users.length && state === "ready" ? <section className="cb-state"><h2 className="cb-section-title">Chưa có người dùng phù hợp</h2></section> : <><div className="cb-admin-table overflow-x-auto"><table><caption className="sr-only">Danh sách người dùng</caption><thead><tr><th>Tên người dùng</th><th>Họ tên</th><th>Email</th><th>Vai trò</th><th>Xác thực email</th></tr></thead><tbody>{users.map((user) => <tr key={user.id}><td>{user.userName}</td><td>{user.fullName}</td><td>{user.email}</td><td>{user.roles.join(", ")}</td><td>{user.emailConfirmed ? "Đã xác thực" : "Chưa xác thực"}</td></tr>)}</tbody></table></div>
      <nav className="flex items-center justify-center gap-3" aria-label="Phân trang người dùng"><button type="button" disabled={page <= 1 || state === "loading"} onClick={() => void load(page - 1)} className="cb-button cb-button-secondary disabled:opacity-50">Trước</button><span aria-current="page">Trang {page} / {result?.totalPages ?? 0}</span><button type="button" disabled={state === "loading" || page >= (result?.totalPages ?? 0)} onClick={() => void load(page + 1)} className="cb-button cb-button-secondary disabled:opacity-50">Sau</button></nav></>}
  </section>;
}
