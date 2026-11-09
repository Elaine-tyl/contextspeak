import { API, ApiError, api } from "./api";
export interface DeletionResult {
  deleted: true;
  cleanupPending: boolean;
}
export function deleteAccount(csrf: string, confirmation: string) {
  if (confirmation !== "DELETE")
    return Promise.reject(
      new Error("Type DELETE to confirm account deletion."),
    );
  return api<DeletionResult>(
    csrf,
    "/v1/account/delete",
    "POST",
    { confirmation },
    { timeoutMs: 130000 },
  );
}
export async function downloadAccount(csrf: string): Promise<Blob> {
  const response = await fetch(API + "/v1/account/export", {
    credentials: "include",
    cache: "no-store",
    headers: { "X-CSRF-Token": csrf },
    signal: AbortSignal.timeout(130000),
  });
  if (!response.ok)
    throw new ApiError(
      response.status,
      response.status === 401
        ? "Your session has expired or changed. Sign in again."
        : "Export could not be completed. Try again.",
    );
  return response.blob();
}
