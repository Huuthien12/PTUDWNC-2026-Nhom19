const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5062";

export class ApiError extends Error {
  constructor(message: string, public status: number) {
    super(message);
    this.name = "ApiError";
  }
}

// Raw transport: auth endpoints must never recursively trigger refresh/retry.
export async function httpClient<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers);
  if (!headers.has("Content-Type") && typeof options.body === "string") {
    headers.set("Content-Type", "application/json");
  }
  const response = await fetch(`${API_URL}${endpoint}`, { ...options, headers });
  if (!response.ok) {
    // Never include request bodies, tokens or server-supplied details in errors/logs.
    throw new ApiError(`API request failed with status ${response.status}`, response.status);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}
