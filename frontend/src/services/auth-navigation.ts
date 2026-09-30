// Only known local destinations, never accept a scheme, query, hash or arbitrary URL.
export function loginDestination(next: string | null | undefined): string {
  return next === "/admin/categories" || next === "/recipes/create" ? next : "/";
}

export function canAccess(roles: string[], requiredRoles: string[]): boolean {
  return requiredRoles.length === 0 || requiredRoles.some((role) => roles.includes(role));
}
