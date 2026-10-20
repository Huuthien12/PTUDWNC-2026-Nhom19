import { revalidateTag } from "next/cache";
import { forwardRecipeRequest } from "@/services/recipe-cache-proxy";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5062";
type Context = { params: Promise<{ path: string[] }> };

async function proxy(request: Request, context: Context) {
  return forwardRecipeRequest(request, (await context.params).path, {
    apiUrl: API_URL, fetch,
    invalidate: tag => revalidateTag(tag, { expire: 0 }),
  });
}

export const GET = proxy;
export const POST = proxy;
export const PUT = proxy;
export const PATCH = proxy;
export const DELETE = proxy;
