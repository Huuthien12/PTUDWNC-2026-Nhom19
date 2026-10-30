"use client";

import { use } from "react";
import RequireAuth from "@/components/auth/RequireAuth";
import EditRecipeForm from "@/components/recipes/EditRecipeForm";
import { recipeRoles } from "@/services/recipe-form";

export default function EditRecipePage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = use(params);
  return <RequireAuth roles={recipeRoles}><EditRecipeForm slug={slug} /></RequireAuth>;
}
