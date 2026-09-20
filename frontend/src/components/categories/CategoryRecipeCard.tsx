import type { RecipeSummary } from "@/types/category";

interface CategoryRecipeCardProps {
  recipe: RecipeSummary;
}

function getDifficultyLabel(
  difficulty: RecipeSummary["difficulty"]
) {
  if (typeof difficulty === "string") {
    return difficulty;
  }

  switch (difficulty) {
    case 0:
      return "Dễ";
    case 1:
      return "Trung bình";
    case 2:
      return "Khó";
    default:
      return "Chưa xác định";
  }
}

export default function CategoryRecipeCard({
  recipe,
}: CategoryRecipeCardProps) {
  return (
    <article className="overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-sm">
      <div className="flex h-44 items-center justify-center bg-orange-50">
        <div className="text-center">
          <div className="text-5xl">🍲</div>

          {recipe.thumbnailUrl && (
            <p className="mt-2 text-xs text-gray-400">
              Có ảnh công thức
            </p>
          )}
        </div>
      </div>

      <div className="p-5">
        <h2 className="text-lg font-bold text-gray-900">
          {recipe.title}
        </h2>

        <p className="mt-2 line-clamp-2 text-sm leading-6 text-gray-600">
          {recipe.description ||
            "Chưa có mô tả cho công thức này."}
        </p>

        <div className="mt-4 grid grid-cols-2 gap-2 text-sm text-gray-600">
          <span>
            Chuẩn bị: {recipe.prepTime} phút
          </span>

          <span>
            Nấu: {recipe.cookTime} phút
          </span>

          <span>
            {recipe.servings} khẩu phần
          </span>

          <span>
            {getDifficultyLabel(recipe.difficulty)}
          </span>
        </div>
      </div>
    </article>
  );
}