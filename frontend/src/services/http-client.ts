import { recipeProxyPath } from "./recipe-cache-policy";
import { clearRecipeRequests } from "./recipe-requests";
import type { ApiErrorKind, ApiProblem } from "@/types/api";

function apiBaseUrl(): string {
  if (typeof window !== "undefined") return process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5062";
  return process.env.API_URL ?? process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5062";
}

export class ApiError extends Error {
  public readonly problem?: ApiProblem;
  public readonly retryAfter?: number;
  public readonly kind: ApiErrorKind;

  constructor(
    message: string,
    public status: number,
    public validationFields: string[] = [],
    options: { problem?: ApiProblem; retryAfter?: number; kind?: ApiErrorKind } = {},
  ) {
    super(message);
    this.name = "ApiError";
    this.kind = options.kind ?? "http";
    this.problem = options.problem;
    this.retryAfter = options.retryAfter;
    // Problem details can contain server validation text; never serialize it accidentally.
    Object.defineProperty(this, "problem", { enumerable: false });
  }
}

// Raw transport: auth endpoints must never recursively trigger refresh/retry.
export type HttpOptions = RequestInit & { onResponse?: (response: Response) => void };

const problemFields = new Set(["type", "title", "status", "detail", "instance", "traceId", "errors"]);

function asRecord(value: unknown): Record<string, unknown> | null {
  return value !== null && typeof value === "object" && !Array.isArray(value)
    ? value as Record<string, unknown> : null;
}

function asString(value: unknown): string | undefined {
  return typeof value === "string" ? value : undefined;
}

function parseValidationErrors(value: unknown): ApiProblem["errors"] {
  const record = asRecord(value);
  if (!record) return undefined;
  const errors = Object.entries(record).flatMap(([field, messages]) => {
    if (!/^[a-zA-Z$.][a-zA-Z0-9$.]{0,99}$/.test(field)) return [];
    const values = (Array.isArray(messages) ? messages : [messages]).filter(
      (message): message is string => typeof message === "string",
    );
    return values.length ? [[field, values] as const] : [];
  });
  return errors.length ? Object.fromEntries(errors) : undefined;
}

async function parseProblem(response: Response): Promise<ApiProblem | undefined> {
  try {
    const text = await response.text();
    if (!text || text.length > 65_536) return undefined;
    const record = asRecord(JSON.parse(text));
    if (!record || !Object.keys(record).some((key) => problemFields.has(key))) return undefined;
    const extensions = Object.fromEntries(Object.entries(record).filter(([key]) =>
      !problemFields.has(key) && key !== "__proto__" && key !== "constructor" && key !== "prototype"));
    return {
      type: asString(record.type), title: asString(record.title),
      status: response.status,
      detail: asString(record.detail), instance: asString(record.instance), traceId: asString(record.traceId),
      errors: parseValidationErrors(record.errors),
      ...(Object.keys(extensions).length ? { extensions } : {}),
    };
  } catch {
    return undefined;
  }
}

export function parseRetryAfter(value: string | null, now = Date.now()): number | undefined {
  if (!value) return undefined;
  const seconds = Number(value.trim());
  if (Number.isFinite(seconds) && seconds >= 0) return Math.ceil(seconds);
  const timestamp = Date.parse(value);
  return Number.isNaN(timestamp) ? undefined : Math.max(0, Math.ceil((timestamp - now) / 1000));
}

function transportError(error: unknown, signal?: AbortSignal | null): ApiError {
  const name = error instanceof Error ? error.name : "";
  const timeout = name === "TimeoutError" || (signal?.aborted && signal.reason instanceof DOMException && signal.reason.name === "TimeoutError");
  if (timeout) return new ApiError("API request timed out", 0, [], { kind: "timeout" });
  if (name === "AbortError" || signal?.aborted) return new ApiError("API request was aborted", 0, [], { kind: "aborted" });
  return new ApiError("API network request failed", 0, [], { kind: "network" });
}

export async function httpClient<T>(endpoint: string, options: HttpOptions = {}): Promise<T> {
  const { onResponse, ...request } = options;
  const headers = new Headers(options.headers);
  if (!headers.has("Content-Type") && typeof options.body === "string") {
    headers.set("Content-Type", "application/json");
  }
  const target = typeof window !== "undefined" && recipeProxyPath(endpoint, request.method ?? "GET")
    ? `/api/recipe-cache${endpoint}` : `${apiBaseUrl()}${endpoint}`;
  let response: Response;
  try {
    response = await fetch(target, { ...request, headers });
  } catch (error) {
    throw transportError(error, request.signal);
  }
  if (!response.ok) {
    const problem = await parseProblem(response);
    const validationFields = Object.keys(problem?.errors ?? {});
    throw new ApiError(`API request failed with status ${response.status}`, response.status, validationFields, {
      problem,
      retryAfter: response.status === 429 ? parseRetryAfter(response.headers.get("Retry-After")) : undefined,
    });
  }
  onResponse?.(response);
  if ((request.method ?? "GET") !== "GET" && recipeProxyPath(endpoint, request.method)) clearRecipeRequests();
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}
