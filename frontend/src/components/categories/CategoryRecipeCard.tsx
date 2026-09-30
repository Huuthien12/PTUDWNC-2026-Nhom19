import Link from "next/link";

import RecipeImage from "@/components/shared/RecipeImage";

import type { RecipeSummary } from "@/types/category";

function difficultyLabel(value: RecipeSummary["difficulty"]) {
  const labels: Record<string, string> = {
    "1": "Dễ",
    "2": "Trung bình",
    "3": "Khó",
    easy: "Dễ",
    medium: "Trung bình",
    hard: "Khó",
  };

  return labels[String(value).toLowerCase()] ?? "Chưa xác định";
}
export default function CategoryRecipeCard({
  recipe,
}: {
  recipe: RecipeSummary;
}) {
  return (
    <Link
      href={`/recipes/${recipe.slug}`}
      className="block"
      aria-label={`Xem chi tiết ${recipe.title}`}
    >
      <article className="cb-recipe">
        <RecipeImage
          src={recipe.thumbnailUrl}
          title={recipe.title}
        />

        <h2>{recipe.title}</h2>

        <p className="cb-recipe-description line-clamp-2">
          {recipe.description ||
            "Một gợi ý cho những bữa ăn đầy cảm hứng."}
        </p>

        <dl className="cb-recipe-meta">
          <div>
            <dt>Chuẩn bị</dt>
            <dd>{recipe.prepTime} phút</dd>
          </div>

          <div>
            <dt>Chế biến</dt>
            <dd>{recipe.cookTime} phút</dd>
          </div>

          <div>
            <dt>Khẩu phần</dt>
            <dd>{recipe.servings} người</dd>
          </div>

          <div>
            <dt>Độ khó</dt>
            <dd>{difficultyLabel(recipe.difficulty)}</dd>
          </div>
        </dl>
      </article>
    </Link>
  );
}
