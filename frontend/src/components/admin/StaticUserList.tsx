type AdminUserPresentationRow = {
  id: string;
  userName: string;
  fullName: string;
  email: string;
  roles: readonly string[];
  emailConfirmed: boolean;
};

// Presentation-only sample rows for Lab 6. They are not an API contract.
const presentationUsers: readonly AdminUserPresentationRow[] = [
  { id: "presentation-admin", userName: "admin", fullName: "Quản trị viên", email: "admin@example.test", roles: ["Admin"], emailConfirmed: true },
  { id: "presentation-author", userName: "author", fullName: "Tác giả mẫu", email: "author@example.test", roles: ["Author"], emailConfirmed: true },
  { id: "presentation-pending", userName: "newcook", fullName: "Người dùng mới", email: "newcook@example.test", roles: ["Author"], emailConfirmed: false },
];

export default function StaticUserList({ users = presentationUsers }: { users?: readonly AdminUserPresentationRow[] }) {
  return <section className="space-y-6">
    <header>
      <h1 className="cb-section-title">Quản lý người dùng</h1>
      <p className="mt-2 text-sm text-gray-600">Bản trình bày tĩnh cho Lab 6; dữ liệu người dùng sẽ được kết nối khi API quản trị sẵn sàng.</p>
    </header>

    <div className="grid gap-3 sm:grid-cols-[1fr_auto]" aria-label="Bộ lọc người dùng (trình bày)">
      <input aria-label="Tìm kiếm người dùng" disabled placeholder="Tìm theo tên, email hoặc tên người dùng" className="w-full rounded-lg border border-gray-300 bg-gray-50 p-3 disabled:cursor-not-allowed" />
      <select aria-label="Lọc theo vai trò" disabled className="rounded-lg border border-gray-300 bg-gray-50 p-3 disabled:cursor-not-allowed">
        <option>Tất cả vai trò</option><option>Admin</option><option>Author</option>
      </select>
    </div>

    {!users.length ? <section className="cb-state" aria-live="polite"><h2 className="cb-section-title">Chưa có người dùng</h2><p>Danh sách người dùng sẽ xuất hiện tại đây khi API quản trị được tích hợp.</p></section> : <>
      <div className="cb-admin-table overflow-x-auto">
        <table>
          <caption className="sr-only">Danh sách người dùng trình bày</caption>
          <thead><tr><th scope="col">Tên người dùng</th><th scope="col">Họ tên</th><th scope="col">Email</th><th scope="col">Vai trò</th><th scope="col">Xác thực email</th></tr></thead>
          <tbody>{users.map((user) => <tr key={user.id}>
            <td>{user.userName}</td><td>{user.fullName}</td><td>{user.email}</td><td>{user.roles.join(", ")}</td><td>{user.emailConfirmed ? "Đã xác thực" : "Chưa xác thực"}</td>
          </tr>)}</tbody>
        </table>
      </div>
      <nav className="flex items-center justify-center gap-3" aria-label="Phân trang người dùng (trình bày)">
        <button type="button" disabled className="cb-button cb-button-secondary disabled:opacity-50">Trước</button>
        <span aria-current="page" className="text-sm font-medium">Trang 1</span>
        <button type="button" disabled className="cb-button cb-button-secondary disabled:opacity-50">Sau</button>
      </nav>
    </>}
  </section>;
}
