import Link from "next/link";
import RecipeImage from "@/components/shared/RecipeImage";
import type { RecipeSummary } from "@/types/recipe";

const difficultyLabels: Record<string, string> = {
  "1": "Dễ",
  "2": "Trung bình",
  "3": "Khó",
  easy: "Dễ",
  medium: "Trung bình",
  hard: "Khó",
};

export default function RecipeCard({ recipe }: { recipe: RecipeSummary }) {
  const difficulty =
    difficultyLabels[String(recipe.difficulty).toLowerCase()] ?? "Chưa xác định";

  return (
    <article className="group flex h-full flex-col overflow-hidden rounded-[28px] border border-[#dfe1d1] bg-white shadow-[0_12px_30px_rgba(37,75,59,0.05)] transition-transform duration-200 hover:-translate-y-1 hover:shadow-[0_18px_40px_rgba(37,75,59,0.08)]">
      <div className="overflow-hidden">
        <RecipeImage src={recipe.thumbnailUrl} title={recipe.title} />
      </div>

      <div className="flex flex-1 flex-col p-5 sm:p-6">
        <div className="mb-3 flex items-center justify-between gap-3 text-[11px] font-semibold uppercase tracking-[0.18em] text-[#254b3b]">
          <span className="inline-flex items-center gap-2">
            <span className="h-2 w-2 rounded-full bg-[#d7b354]" aria-hidden="true" />
            Gợi ý bếp
          </span>
          <span className="rounded-full border border-[#dfe1d1] bg-[#f8f6f0] px-2 py-1 text-[10px] tracking-[0.12em] text-[#4d5a4b]">
            {difficulty}
          </span>
        </div>

        <h2 className="font-serif text-[1.9rem] leading-tight text-[#1d2b22]">
          {recipe.title}
        </h2>

        <p className="mt-3 line-clamp-3 text-sm leading-6 text-[#5d655a]">
          {recipe.description || "Một món ăn dễ làm, giàu hương vị và phù hợp cho bữa ăn hàng ngày."}
        </p>

        <dl className="mt-5 grid grid-cols-3 gap-3 border-t border-[#eae7dc] pt-4">
          <div>
            <dt className="text-[10px] uppercase tracking-[0.18em] text-[#6a7068]">Chuẩn bị</dt>
            <dd className="mt-1 text-sm font-semibold text-[#1d2b22]">{recipe.prepTime} phút</dd>
          </div>
          <div>
            <dt className="text-[10px] uppercase tracking-[0.18em] text-[#6a7068]">Nấu</dt>
            <dd className="mt-1 text-sm font-semibold text-[#1d2b22]">{recipe.cookTime} phút</dd>
          </div>
          <div>
            <dt className="text-[10px] uppercase tracking-[0.18em] text-[#6a7068]">Khẩu phần</dt>
            <dd className="mt-1 text-sm font-semibold text-[#1d2b22]">{recipe.servings}</dd>
          </div>
        </dl>

        <Link
          href="/recipes"
          className="mt-5 inline-flex items-center gap-2 text-sm font-semibold text-[#254b3b] transition-colors hover:text-[#18382b]"
        >
          Xem công thức
          <span aria-hidden="true">→</span>
        </Link>
      </div>
    </article>
  );
}
