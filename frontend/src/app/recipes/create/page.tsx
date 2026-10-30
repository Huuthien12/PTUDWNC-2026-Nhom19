import RequireAuth from "@/components/auth/RequireAuth";
import CreateRecipeForm from "@/components/recipes/CreateRecipeForm";
import { recipeRoles } from "@/services/recipe-form";

export default function CreateRecipePage() {
  return <RequireAuth roles={recipeRoles}><CreateRecipeForm /></RequireAuth>;
}
