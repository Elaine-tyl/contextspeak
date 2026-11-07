export const API = "http://127.0.0.1:4318";
export async function api<T>(
  token: string,
  path: string,
  method = "GET",
  body?: unknown,
): Promise<T> {
  const response = await fetch(API + path, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      ...(body === undefined ? {} : { "Content-Type": "application/json" }),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: AbortSignal.timeout(6000),
  });
  if (!response.ok)
    throw new Error(
      response.status === 401
        ? "Your developer token has expired. Connect again."
        : `Request failed (${response.status}). Try again.`,
    );
  return response.json();
}
