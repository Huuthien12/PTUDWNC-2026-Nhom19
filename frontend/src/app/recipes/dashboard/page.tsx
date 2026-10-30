import RequireAuth from "@/components/auth/RequireAuth";
import AuthorRecipeDashboard from "@/components/recipes/AuthorRecipeDashboard";
import { recipeRoles } from "@/services/recipe-form";

export default function RecipeDashboardPage() {
  return <RequireAuth roles={recipeRoles}><AuthorRecipeDashboard /></RequireAuth>;
}
