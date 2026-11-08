export const API =
  process.env.NEXT_PUBLIC_API_ORIGIN ?? "http://127.0.0.1:4318";
export interface WebSession {
  identity: { issuer: string; subject: string };
  csrfToken: string;
  expiresAt: string;
}
export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
  }
}
export async function api<T>(
  token: string,
  path: string,
  method = "GET",
  body?: unknown,
  options: { signal?: AbortSignal; timeoutMs?: number } = {},
): Promise<T> {
  const response = await fetch(API + path, {
    method,
    credentials: "include",
    cache: "no-store",
    headers: {
      ...(token ? { "X-CSRF-Token": token } : {}),
      ...(body === undefined ? {} : { "Content-Type": "application/json" }),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: options.signal
      ? AbortSignal.any([
          options.signal,
          AbortSignal.timeout(options.timeoutMs ?? 6000),
        ])
      : AbortSignal.timeout(options.timeoutMs ?? 6000),
  });
  if (!response.ok)
    throw new ApiError(
      response.status,
      response.status === 401
        ? "Your session has expired or changed. Sign in again."
        : `Request failed (${response.status}). Try again.`,
    );
  return response.json();
}
