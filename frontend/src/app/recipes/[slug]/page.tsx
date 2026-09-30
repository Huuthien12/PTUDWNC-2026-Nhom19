import { notFound } from "next/navigation";
import { getRecipeBySlug } from "@/services/recipe-service";
import type { RecipeDifficulty, RecipeDetail } from "@/types/recipe";

type PageProps = {
  params: Promise<{
    slug: string;
  }>;
};

function getDifficultyLabel(difficulty: RecipeDifficulty) {
  switch (difficulty) {
    case 1:
      return "Dễ";
    case 2:
      return "Trung bình";
    case 3:
      return "Khó";
    default:
      return "Không xác định";
  }
}

function formatNumber(value: number | null | undefined) {
  if (value === null || value === undefined) {
    return "-";
  }

  return value.toString();
}

export default async function RecipeDetailPage({
  params,
}: PageProps) {
  const { slug } = await params;

  let recipe: RecipeDetail | null;

  try {
    recipe = await getRecipeBySlug(slug);
  } catch {
    throw new Error("Không thể tải thông tin công thức.");
  }

  if (!recipe) {
    notFound();
  }

  const primaryImage =
    recipe.images.find((image) => image.isPrimary) ??
    recipe.images[0];

  return (
    <main style={{ maxWidth: 1100, margin: "0 auto", padding: "40px 24px" }}>
      <article>
        <header style={{ marginBottom: 32 }}>
          <p style={{ marginBottom: 8, opacity: 0.7 }}>
            {recipe.category.name}
          </p>

          <h1 style={{ fontSize: 40, marginBottom: 16 }}>
            {recipe.title}
          </h1>

          <p style={{ fontSize: 18, lineHeight: 1.6 }}>
            {recipe.description}
          </p>

          <div
            style={{
              display: "flex",
              flexWrap: "wrap",
              gap: 20,
              marginTop: 20,
            }}
          >
            <span>Chuẩn bị: {recipe.prepTime} phút</span>
            <span>Nấu: {recipe.cookTime} phút</span>
            <span>Khẩu phần: {recipe.servings}</span>
            <span>
              Độ khó: {getDifficultyLabel(recipe.difficulty)}
            </span>
          </div>
        </header>

        {primaryImage && (
          <div style={{ marginBottom: 40 }}>
            <img
              src={
                primaryImage.mediumUrl ??
                primaryImage.originalUrl
              }
              alt={primaryImage.altText ?? recipe.title}
              style={{
                width: "100%",
                maxHeight: 500,
                objectFit: "cover",
                borderRadius: 12,
              }}
            />
          </div>
        )}

        <section style={{ marginBottom: 40 }}>
          <h2>Thông tin tác giả</h2>

          <p>
            <strong>{recipe.author.fullName}</strong>
          </p>

          {recipe.author.avatarUrl && (
            <img
              src={recipe.author.avatarUrl}
              alt={recipe.author.fullName}
              width={64}
              height={64}
              style={{ borderRadius: "50%", objectFit: "cover" }}
            />
          )}
        </section>

        <section style={{ marginBottom: 40 }}>
          <h2>Nguyên liệu</h2>

          {recipe.ingredients.length === 0 ? (
            <p>Chưa có thông tin nguyên liệu.</p>
          ) : (
            <ul>
              {recipe.ingredients.map((ingredient) => (
                <li key={ingredient.id} style={{ marginBottom: 8 }}>
                  <strong>{ingredient.name}</strong>
                  {ingredient.quantity !== null &&
                    ingredient.quantity !== undefined &&
                    ` - ${formatNumber(ingredient.quantity)}`}
                  {ingredient.unit && ` ${ingredient.unit}`}
                  {ingredient.notes && ` (${ingredient.notes})`}
                </li>
              ))}
            </ul>
          )}
        </section>

        <section style={{ marginBottom: 40 }}>
          <h2>Các bước chế biến</h2>

          {recipe.steps.length === 0 ? (
            <p>Chưa có thông tin các bước chế biến.</p>
          ) : (
            <ol>
              {recipe.steps.map((step) => (
                <li key={step.id} style={{ marginBottom: 24 }}>
                  <h3>
                    Bước {step.stepNumber}
                    {step.title ? `: ${step.title}` : ""}
                  </h3>

                  <p style={{ lineHeight: 1.7 }}>
                    {step.description}
                  </p>

                  {step.timerMinutes !== null &&
                    step.timerMinutes !== undefined && (
                      <p>
                        Thời gian: {step.timerMinutes} phút
                      </p>
                    )}

                  {step.imageUrl && (
                    <img
                      src={step.imageUrl}
                      alt={step.title ?? `Bước ${step.stepNumber}`}
                      style={{
                        maxWidth: "100%",
                        maxHeight: 400,
                        objectFit: "cover",
                        borderRadius: 8,
                      }}
                    />
                  )}
                </li>
              ))}
            </ol>
          )}
        </section>

        <section style={{ marginBottom: 40 }}>
          <h2>Thông tin dinh dưỡng</h2>

          {!recipe.nutrition ? (
            <p>Chưa có thông tin dinh dưỡng.</p>
          ) : (
            <div
              style={{
                display: "grid",
                gridTemplateColumns:
                  "repeat(auto-fit, minmax(150px, 1fr))",
                gap: 16,
              }}
            >
              <div>
                <strong>Calo</strong>
                <p>{formatNumber(recipe.nutrition.calories)} kcal</p>
              </div>

              <div>
                <strong>Protein</strong>
                <p>{formatNumber(recipe.nutrition.protein)} g</p>
              </div>

              <div>
                <strong>Carbs</strong>
                <p>{formatNumber(recipe.nutrition.carbs)} g</p>
              </div>

              <div>
                <strong>Chất béo</strong>
                <p>{formatNumber(recipe.nutrition.fat)} g</p>
              </div>
            </div>
          )}
        </section>

        <section style={{ marginBottom: 40 }}>
          <h2>Hướng dẫn chung</h2>
          <p style={{ whiteSpace: "pre-line", lineHeight: 1.8 }}>
            {recipe.instructions}
          </p>
        </section>
      </article>
    </main>
  );
}