import Link from "next/link";
import ContentState from "@/components/shared/ContentState";
import RecipeCard from "@/components/recipes/RecipeCard";
import RecipePagination from "@/components/recipes/RecipePagination";
import { getRecipes } from "@/services/recipe-service";
import type { RecipePage } from "@/types/recipe";

interface RecipesPageProps { searchParams: Promise<{ page?: string }> }

const recipeFilters = [
  "Tất cả",
  "Món chính",
  "Món phụ",
  "Bữa sáng",
  "Bánh",
  "Món chay",
];

function parsePage(value?: string) {
  const page = Number(value ?? "1");
  return Number.isInteger(page) && page >= 1 ? page : 1;
}

export default async function RecipesPage({ searchParams }: RecipesPageProps) {
  const { page: pageValue } = await searchParams;
  let recipes: RecipePage;
  try {
    recipes = await getRecipes(parsePage(pageValue), 12);
  } catch {
    return <div className="cb-container cb-section"><ContentState title="Chưa thể tải công thức" description="Đã có lỗi khi tải dữ liệu. Vui lòng thử lại sau." retry /></div>;
  }

  const featuredRecipe = recipes.items[0];

  return (
    <>
      <section className="bg-[#f4f0e4] py-10 sm:py-16">
        <div className="cb-container">
          <div className="grid gap-8 lg:grid-cols-[1.5fr_0.9fr] lg:items-end">
            <div>
              <p className="text-[11px] font-semibold uppercase tracking-[0.22em] text-[#254b3b]">Góc công thức</p>
              <h1 className="mt-4 font-serif text-[2.7rem] leading-none text-[#1d2b22] sm:text-[4rem] lg:text-[5rem]">
                Easy recipes
              </h1>
              <p className="mt-5 max-w-xl text-base leading-7 text-[#5d655a] sm:text-lg">
                Những món ăn ngon, dễ làm và phù hợp cho mỗi ngày. Từ món chính đến món phụ, từ bữa ăn nhanh đến đồ ngọt, tất cả đều có ở đây.
              </p>
            </div>

            <div className="rounded-[30px] border border-[#dfe1d1] bg-white p-5 shadow-[0_12px_35px_rgba(37,75,59,0.06)] sm:p-6">
              <p className="text-[11px] font-semibold uppercase tracking-[0.2em] text-[#254b3b]">Món được xem nhiều</p>
              <h2 className="mt-3 font-serif text-3xl leading-tight text-[#1d2b22]">
                {featuredRecipe?.title ?? "Công thức mới"}
              </h2>
              <p className="mt-3 text-sm leading-6 text-[#5d655a]">
                {featuredRecipe?.description ?? "Khám phá những món ăn dễ làm cho cả gia đình."}
              </p>
              <div className="mt-5 flex items-center gap-3">
                <Link href="/recipes" className="inline-flex items-center justify-center rounded-full bg-[#254b3b] px-5 py-3 text-sm font-semibold text-white transition hover:bg-[#18382b]">
                  Khám phá ngay
                </Link>
                <span className="text-sm font-medium text-[#5d655a]">{recipes.totalCount} công thức</span>
              </div>
            </div>
          </div>
        </div>
      </section>

      <section className="cb-container cb-section" aria-labelledby="recipes-heading">
        <div className="mb-8 flex flex-col gap-5 border-b border-[#dfe1d1] pb-6 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <p className="text-[11px] font-semibold uppercase tracking-[0.2em] text-[#254b3b]">Recipe collection</p>
            <h2 id="recipes-heading" className="mt-2 font-serif text-3xl text-[#1d2b22] sm:text-4xl">
              Danh sách món ăn
            </h2>
          </div>

          <div className="flex flex-wrap gap-2">
            {recipeFilters.map((filter) => (
              <button
                key={filter}
                type="button"
                className={`rounded-full border px-4 py-2 text-sm font-medium transition ${
                  filter === "Tất cả"
                    ? "border-[#254b3b] bg-[#254b3b] text-white"
                    : "border-[#dfe1d1] bg-white text-[#254b3b] hover:border-[#254b3b]"
                }`}
              >
                {filter}
              </button>
            ))}
          </div>
        </div>

        {recipes.items.length === 0 ? (
          <div className="cb-state">
            <h2 className="cb-section-title">Chưa có công thức</h2>
            <p>Các công thức được xuất bản sẽ xuất hiện tại đây.</p>
          </div>
        ) : (
          <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">
            {recipes.items.map((recipe) => (
              <RecipeCard key={recipe.id} recipe={recipe} />
            ))}
          </div>
        )}

        <RecipePagination currentPage={recipes.page} totalPages={recipes.totalPages} />
      </section>
    </>
  );
}
