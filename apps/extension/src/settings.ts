import { browser } from "wxt/browser";
import type {
  CollectionSettings,
  TranslationPreferences,
} from "@contextspeak/contracts";
export const API =
  (import.meta.env as unknown as Record<string, string | undefined>)
    .WXT_API_ORIGIN ?? "http://127.0.0.1:4318";
export const WEB =
  (import.meta.env as unknown as Record<string, string | undefined>)
    .WXT_WEB_ORIGIN ?? "http://127.0.0.1:3000";
export interface Connection {
  translationPreferences?: TranslationPreferences;
  token: string;
  identity: string;
  enabled: boolean;
  excludedOrigins: string[];
}
export const disconnected: Connection = {
  token: "",
  identity: "",
  enabled: false,
  excludedOrigins: [],
};
export async function connection(): Promise<Connection> {
  return (
    ((await browser.storage.local.get("connection")).connection as
      Connection | undefined) ?? disconnected
  );
}
export async function api<T>(
  path: string,
  method = "GET",
  body?: unknown,
  token?: string,
  signal?: AbortSignal,
): Promise<T> {
  const auth = token ?? (await connection()).token;
  const response = await fetch(API + path, {
    method,
    credentials: "omit",
    headers: {
      Authorization: `Bearer ${auth}`,
      ...(body === undefined ? {} : { "Content-Type": "application/json" }),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: signal
      ? AbortSignal.any([
          signal,
          AbortSignal.timeout(path === "/v1/translations" ? 25000 : 5000),
        ])
      : AbortSignal.timeout(path === "/v1/translations" ? 25000 : 5000),
  });
  if (!response.ok) {
    const details = await response.json().catch(() => ({}));
    throw Object.assign(new Error(`Request failed (${response.status})`), {
      status: response.status,
      code:
        typeof details.message === "string"
          ? details.message
          : "PROVIDER_UNAVAILABLE",
    });
  }
  return response.json();
}
export async function verifiedIdentity(token: string) {
  const result = await api<{ issuer: string; subject: string }>(
    "/v1/identity",
    "GET",
    undefined,
    token,
  );
  return JSON.stringify([result.issuer, result.subject]);
}
export async function serverSettings(signal?: AbortSignal) {
  return api<CollectionSettings>(
    "/v1/settings",
    "GET",
    undefined,
    undefined,
    signal,
  );
}
