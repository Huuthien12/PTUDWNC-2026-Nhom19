import Link from "next/link";
import { Suspense } from "react";
import RecipeCard from "@/components/recipes/RecipeCard";
import { getCategories } from "@/services/category-service";
import { getRecipes } from "@/services/recipe-service";
import type { Category } from "@/types/category";
import type { RecipePage } from "@/types/recipe";

async function CategorySection() {
  let categories: Category[] = [];
  let categoriesFailed = false;

  try {
    categories = await getCategories();
  } catch {
    categoriesFailed = true;
  }

  return (
    <section aria-labelledby="home-categories-heading" className="rounded-3xl border border-[#d8d9cb] bg-white p-6 shadow-[0_12px_28px_rgba(37,75,59,.05)]">
      <div className="mb-5 flex flex-wrap items-end justify-between gap-3 border-b border-[#eae7dc] pb-4">
        <div>
          <p className="text-xs font-bold uppercase tracking-[0.18em] text-[#254b3b]">Bắt đầu từ đây</p>
          <h2 id="home-categories-heading" className="mt-1 font-serif text-2xl text-[#1d2b22] sm:text-3xl">Khám phá theo khẩu vị</h2>
        </div>
        <Link href="/categories" className="text-sm font-semibold text-[#254b3b] hover:text-[#18382b]">Xem tất cả →</Link>
      </div>
      {categoriesFailed ? (
        <div className="rounded-2xl border border-red-200 bg-red-50 p-4 text-red-800" role="alert">
          <p className="font-semibold">Chưa thể tải danh mục.</p>
          <p className="mt-1 text-sm">Vui lòng tải lại trang hoặc thử lại sau.</p>
        </div>
      ) : categories.length === 0 ? (
        <div className="rounded-2xl border border-[#d8d9cb] bg-[#faf8f2] p-4 text-[#61695c]" role="status">
          Chưa có danh mục để hiển thị.
        </div>
      ) : (
        <ul className="grid gap-2.5" aria-label="Danh sách danh mục">
          {categories.map((category, index) => (
            <li key={category.id}>
              <Link href={`/categories/${category.slug}`} className="group flex items-center justify-between gap-3 rounded-2xl border border-[#e6e4d8] bg-[#faf8f2] px-4 py-3 transition hover:border-[#254b3b] hover:bg-white">
                <span className="flex min-w-0 items-center gap-3">
                  <span className="text-xl" aria-hidden="true">{["🍲", "☀️", "🌿", "🍰", "🥗", "🍜", "🥘", "🧁"][index % 8]}</span>
                  <span className="truncate font-serif text-lg text-[#1d2b22] group-hover:text-[#254b3b]">{category.name}</span>
                </span>
                <span className="shrink-0 rounded-full bg-[#e9efe6] px-2.5 py-0.5 text-xs font-semibold text-[#254b3b]">
                  {category.recipeCount}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

async function LatestRecipesSection() {
  let recipes: RecipePage | null = null;
  try {
    recipes = await getRecipes(1, 6, { sortBy: "createdAt", sortOrder: "desc" });
  } catch { /* Render the section-level error below. */ }

  if (!recipes) {
    return (
      <section aria-labelledby="latest-recipes-heading">
        <h2 id="latest-recipes-heading" className="mb-7 font-serif text-3xl text-[#1d2b22] sm:text-4xl">Công thức mới nhất</h2>
        <div className="rounded-3xl border border-red-200 bg-red-50 p-6 text-red-800" role="alert">
          <p className="font-semibold">Chưa thể tải công thức mới.</p>
          <p className="mt-2 text-sm">Vui lòng tải lại trang hoặc thử lại sau.</p>
        </div>
      </section>
    );
  }

  return (
    <section aria-labelledby="latest-recipes-heading">
      <div className="mb-7 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-xs font-bold uppercase tracking-[0.18em] text-[#254b3b]">Vừa ra lò</p>
          <h2 id="latest-recipes-heading" className="mt-2 font-serif text-3xl text-[#1d2b22] sm:text-4xl">Công thức mới nhất</h2>
        </div>
        <Link href="/recipes?sort=createdAt-desc" className="text-sm font-semibold text-[#254b3b] hover:text-[#18382b]">Xem tất cả →</Link>
      </div>
      {recipes.items.length === 0 ? (
        <div className="rounded-3xl border border-[#d8d9cb] bg-white p-6 text-[#61695c]" role="status">
          Chưa có công thức để hiển thị.
        </div>
      ) : (
        <div className="grid gap-6 md:grid-cols-2">
          {recipes.items.map((recipe) => <RecipeCard key={recipe.id} recipe={recipe} />)}
        </div>
      )}
    </section>
  );
}

const sectionLoading = (label: string) => (
  <p className="py-6 text-[#61695c]" role="status">{label}</p>
);

export default function Home() {
  return (
    <main className="bg-[#faf8f2] text-[#262b25]">
      <section className="border-b border-[#d8d9cb] bg-[#f0ecdf]">
        <div className="mx-auto grid min-h-[520px] w-[min(100%-3rem,1160px)] items-center gap-10 py-16 lg:grid-cols-[1.05fr_.95fr] lg:py-20">
          <div>
            <p className="text-xs font-bold uppercase tracking-[0.2em] text-[#254b3b]">Culinary Blog · Cảm hứng mỗi ngày</p>
            <h1 className="mt-5 max-w-2xl font-serif text-5xl leading-[1.05] tracking-[-0.04em] text-[#1d2b22] sm:text-7xl">
              Bữa cơm ngon bắt đầu từ một ý tưởng.
            </h1>
            <p className="mt-6 max-w-xl text-lg leading-8 text-[#61695c]">
              Tìm công thức phù hợp với nguyên liệu bạn có và biến căn bếp thành nơi kể những câu chuyện thật ngon.
            </p>
            <div className="mt-8 flex flex-wrap items-center gap-5 text-sm font-semibold text-[#254b3b]">
              <Link href="/recipes" className="rounded-full bg-[#254b3b] px-5 py-3 text-white hover:bg-[#18382b]">Xem công thức <span aria-hidden="true">→</span></Link>
              <Link href="/categories" className="border-b border-[#d7b354] pb-1 hover:text-[#18382b]">Khám phá theo danh mục</Link>
            </div>
          </div>
          <div className="relative mx-auto w-full max-w-md">
            <div className="aspect-square rounded-[42%_58%_55%_45%/45%_42%_58%_55%] bg-[#254b3b] p-8 shadow-[0_24px_60px_rgba(37,75,59,.16)] sm:p-12">
              <div className="flex h-full flex-col justify-between rounded-[32%_68%_42%_58%/58%_35%_65%_42%] border border-[#d7b354]/50 bg-[#315d49] p-8 text-white sm:p-10">
                <span className="text-6xl text-[#d7b354]" aria-hidden="true">“</span>
                <p className="font-serif text-3xl leading-tight sm:text-4xl">Nấu ăn chậm lại, thưởng thức nhiều hơn.</p>
                <span className="text-xs font-bold uppercase tracking-[0.2em] text-[#f3d98b]">Chuyện ngon mỗi ngày</span>
              </div>
            </div>
          </div>
        </div>
      </section>

      <div className="mx-auto grid w-[min(100%-3rem,1160px)] items-start gap-8 py-12 sm:py-16 lg:grid-cols-[320px_minmax(0,1fr)]">
        <aside aria-label="Danh mục món ăn" className="lg:sticky lg:top-24">
          <Suspense fallback={sectionLoading("Đang tải danh mục...")}><CategorySection /></Suspense>
        </aside>
        <div className="min-w-0">
          <Suspense fallback={sectionLoading("Đang tải công thức mới...")}><LatestRecipesSection /></Suspense>
        </div>
      </div>

      <section className="mx-auto mb-12 w-[min(100%-3rem,1160px)] rounded-3xl bg-[#dce4d3] px-6 py-8 sm:px-10">
        <div className="flex flex-wrap items-center justify-between gap-5">
          <div>
            <p className="text-xs font-bold uppercase tracking-[0.18em] text-[#254b3b]">Góc bếp của bạn</p>
            <h2 className="mt-2 font-serif text-3xl text-[#1d2b22]">Lưu lại những công thức yêu thích</h2>
          </div>
          <Link href="/recipes" className="rounded-full border border-[#254b3b] px-5 py-3 text-sm font-semibold text-[#254b3b] hover:bg-[#254b3b] hover:text-white">Mở danh sách món ăn</Link>
        </div>
      </section>

    </main>
  );
}
