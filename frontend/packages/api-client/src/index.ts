// API types will be generated from the backend OpenAPI document (`pnpm generate:api`).
// Until then this package only holds the transport layer shared by both apps.

/** RFC 7807 problem details; `code` is the machine-readable error code translated via i18n. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails | undefined;

  constructor(status: number, problem: ProblemDetails | undefined) {
    super(problem?.code ?? `http_${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api${path}`, {
    ...init,
    headers: { Accept: 'application/json', ...init?.headers },
  });

  if (!response.ok) {
    const isProblem = response.headers.get('Content-Type')?.includes('json') ?? false;
    throw new ApiError(response.status, isProblem ? ((await response.json()) as ProblemDetails) : undefined);
  }

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T);
}
