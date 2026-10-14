// Coalesce concurrent public reads only. Completed responses are never retained here.
const pending = new Map<string, Promise<unknown>>();
export function publicRecipeRequest<T>(key: string, read: () => Promise<T>): Promise<T> {
  const existing = pending.get(key);
  if (existing) return existing as Promise<T>;
  const result = read();
  pending.set(key, result);
  void result.finally(() => { if (pending.get(key) === result) pending.delete(key); }).catch(() => {});
  return result;
}
export function clearRecipeRequests() { pending.clear(); }
