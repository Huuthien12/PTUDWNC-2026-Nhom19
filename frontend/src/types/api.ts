export interface ApiProblem {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors?: Readonly<Record<string, readonly string[]>>;
  extensions?: Readonly<Record<string, unknown>>;
}

export type ApiErrorKind = "http" | "network" | "timeout" | "aborted";

export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
