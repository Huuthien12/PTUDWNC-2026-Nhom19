const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5062";

export class ApiError extends Error {
  constructor(message: string, public status: number, public validationFields: string[] = []) {
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
    let validationFields: string[] = [];
    if (response.status === 400) {
      try {
        const body: unknown = await response.json();
        if (body && typeof body === "object" && "errors" in body &&
            body.errors && typeof body.errors === "object" && !Array.isArray(body.errors)) {
          // Keep only field paths, never messages/details that could contain secrets.
          validationFields = Object.keys(body.errors).filter((key) => /^[a-zA-Z$.][a-zA-Z0-9$.]{0,99}$/.test(key));
        }
      } catch { /* Binding errors can have an empty or non-JSON body. */ }
    }
    throw new ApiError(`API request failed with status ${response.status}`, response.status, validationFields);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}
