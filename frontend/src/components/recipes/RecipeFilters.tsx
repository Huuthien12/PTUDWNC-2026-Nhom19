import Link from "next/link";
import type { Category } from "@/types/category";
import type { RecipeListFilters } from "@/types/recipe";

export default function RecipeFilters({
  categories,
  categoriesFailed,
  filters,
}: {
  categories: Category[];
  categoriesFailed: boolean;
  filters: RecipeListFilters;
}) {
  const selectedSort = `${filters.sortBy ?? "createdAt"}-${filters.sortOrder ?? "desc"}`;

  return (
    <form action="/recipes" method="get" className="mb-8 rounded-2xl border border-[#d8d9cb] bg-white p-5">
      <input type="hidden" name="page" value="1" />
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <label className="grid gap-2 text-sm font-semibold text-[#254b3b]" htmlFor="recipe-category">
          Danh mục
          <select id="recipe-category" name="categoryId" defaultValue={filters.categoryId ?? ""} className="rounded-xl border border-[#d8d9cb] bg-white px-3 py-2 font-normal text-[#262b25]">
            <option value="">Tất cả danh mục</option>
            {categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}
          </select>
        </label>

        <label className="grid gap-2 text-sm font-semibold text-[#254b3b]" htmlFor="recipe-difficulty">
          Độ khó
          <select id="recipe-difficulty" name="difficulty" defaultValue={filters.difficulty === undefined ? "" : String(filters.difficulty)} className="rounded-xl border border-[#d8d9cb] bg-white px-3 py-2 font-normal text-[#262b25]">
            <option value="">Mọi mức độ</option>
            <option value="1">Dễ</option>
            <option value="2">Trung bình</option>
            <option value="3">Khó</option>
          </select>
        </label>

        <label className="grid gap-2 text-sm font-semibold text-[#254b3b]" htmlFor="recipe-cook-time">
          Thời gian nấu tối đa (phút)
          <input id="recipe-cook-time" name="maxCookTime" type="number" min="0" step="1" defaultValue={filters.maxCookTime ?? ""} placeholder="Ví dụ: 30" className="rounded-xl border border-[#d8d9cb] bg-white px-3 py-2 font-normal text-[#262b25]" />
        </label>

        <label className="grid gap-2 text-sm font-semibold text-[#254b3b]" htmlFor="recipe-sort">
          Sắp xếp
          <select id="recipe-sort" name="sort" defaultValue={selectedSort} className="rounded-xl border border-[#d8d9cb] bg-white px-3 py-2 font-normal text-[#262b25]">
            <option value="createdAt-desc">Mới nhất</option>
            <option value="createdAt-asc">Cũ nhất</option>
            <option value="title-asc">Tên A–Z</option>
            <option value="title-desc">Tên Z–A</option>
          </select>
        </label>
        <label className="grid gap-2 text-sm font-semibold text-[#254b3b]" htmlFor="recipe-servings">
          Khẩu phần tối thiểu
          <input id="recipe-servings" name="minServings" type="number" min="1" step="1" defaultValue={filters.minServings ?? ""} className="rounded-xl border border-[#d8d9cb] bg-white px-3 py-2 font-normal text-[#262b25]" />
        </label>
      </div>

      {categoriesFailed && <p className="mt-3 text-sm text-red-700" role="alert">Chưa thể tải danh mục. Các bộ lọc khác vẫn có thể sử dụng.</p>}

      <div className="mt-5 flex flex-wrap gap-3">
        <button type="submit" className="cb-button">Áp dụng bộ lọc</button>
        <Link className="cb-button cb-button-secondary" href="/recipes">Xóa bộ lọc</Link>
      </div>
    </form>
  );
}
