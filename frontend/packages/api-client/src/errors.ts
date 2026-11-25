/** RFC 7807 problem details; `code` is the machine-readable error code translated via i18n. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  /** Field path (e.g. `address.postalCode`) → validation error codes (e.g. `validation.postal_code`). */
  errors?: Record<string, string[]>;
}

export const NETWORK_ERROR_CODE = 'common.network';
export const UNEXPECTED_ERROR_CODE = 'common.unexpected';

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails | undefined;

  constructor(status: number, problem: ProblemDetails | undefined) {
    super(problem?.code ?? `http_${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }

  /** Error code to translate; falls back to a generic one when the server sent none. */
  get code(): string {
    return this.problem?.code ?? UNEXPECTED_ERROR_CODE;
  }

  get fieldErrors(): Record<string, string[]> {
    return this.problem?.errors ?? {};
  }

  static async fromResponse(response: Response): Promise<ApiError> {
    return new ApiError(response.status, await readProblem(response));
  }
}

/** Error code of anything thrown by an API call — including network failures (fetch rejects with TypeError). */
export function errorCodeOf(error: unknown): string {
  if (error instanceof ApiError) {
    return error.code;
  }
  return error instanceof TypeError ? NETWORK_ERROR_CODE : UNEXPECTED_ERROR_CODE;
}

async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  if (!(response.headers.get('Content-Type') ?? '').includes('json')) {
    return undefined;
  }
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    return undefined;
  }
}
