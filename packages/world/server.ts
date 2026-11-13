import { createServer, type IncomingMessage } from "node:http";
import type { AddressInfo } from "node:net";
import { randomBytes, timingSafeEqual } from "node:crypto";
import { readFile, realpath } from "node:fs/promises";
import { extname, resolve, sep } from "node:path";
import { z } from "zod";
import { intentSchema, normalizeWave, replySchema } from "./protocol";
import { conversationInput, conversationOutput } from "./conversation";
import { roleplayInput, roleplayOutput } from "./roleplay";
import { lifeInput, lifeOutput } from "./life";
import { sliceInput, sliceOutput } from "./slice";

interface Options {
  roomOrigin?: string;
  webRoot: string;
  macArchive?: string;
  transcribe: (audio: Buffer, signal: AbortSignal) => Promise<string>;
  interpret: (
    input: z.infer<typeof replySchema>,
    signal: AbortSignal,
  ) => Promise<unknown>;
  converse: (
    input: z.infer<typeof conversationInput>,
    signal: AbortSignal,
  ) => Promise<unknown>;
  roleplay?: (
    input: z.infer<typeof roleplayInput>,
    signal: AbortSignal,
  ) => Promise<unknown>;
  lifeReview?: (
    input: z.infer<typeof lifeInput>,
    signal: AbortSignal,
  ) => Promise<unknown>;
  sliceReview?: (
    input: z.infer<typeof sliceInput>,
    signal: AbortSignal,
  ) => Promise<unknown>;
  speak: (text: string, signal: AbortSignal) => Promise<Buffer>;
}
class HttpFailure extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
  }
}
async function body(request: IncomingMessage) {
  const chunks: Buffer[] = [];
  let length = 0;
  for await (const chunk of request) {
    length += chunk.length;
    if (length > 8 * 1024 * 1024)
      throw new HttpFailure(413, "Record a shorter reply.");
    chunks.push(chunk);
  }
  return Buffer.concat(chunks);
}
const types: Record<string, string> = {
  ".html": "text/html; charset=utf-8",
  ".js": "application/javascript",
  ".css": "text/css",
  ".data": "application/octet-stream",
  ".bundle": "application/octet-stream",
  ".bin": "application/octet-stream",
  ".hash": "text/plain",
  ".json": "application/json",
  ".wasm": "application/wasm",
  ".pck": "application/octet-stream",
  ".png": "image/png",
  ".svg": "image/svg+xml",
  ".zip": "application/zip",
};

/** A local guest runtime, not a public inference server or account API. */
export function createWorldServer(options: Options) {
  const roomUrl = new URL(options.roomOrigin ?? "http://127.0.0.1:4330");
  if (
    !["http:", "https:"].includes(roomUrl.protocol) ||
    roomUrl.username ||
    roomUrl.password
  )
    throw new Error("Use an HTTP(S) room origin without credentials.");
  const roomOrigin = roomUrl.origin;
  const token = randomBytes(32).toString("base64url");
  let active = 0;
  const server = createServer(async (request, response) => {
    const port = (server.address() as AddressInfo).port;
    const origin = `http://127.0.0.1:${port}`;
    const native =
      !request.headers.origin &&
      !request.headers["sec-fetch-site"] &&
      request.headers["x-contextspeak-client"] === "native";
    const sameOrigin =
      request.headers.origin === origin ||
      (!request.headers.origin &&
        request.headers["sec-fetch-site"] === "same-origin");
    const json = (status: number, value: unknown) => {
      response.writeHead(status, {
        "Content-Type": "application/json",
        "Cache-Control": "no-store",
      });
      response.end(JSON.stringify(value));
    };
    response.setHeader("X-Content-Type-Options", "nosniff");
    response.setHeader(
      "Content-Security-Policy",
      `default-src 'self'; script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval'; worker-src 'self' blob:; connect-src 'self' ${roomOrigin}; img-src 'self' data: blob:; style-src 'self' 'unsafe-inline'; frame-ancestors 'self' http://127.0.0.1:3000`,
    );
    try {
      if (request.headers.host !== `127.0.0.1:${port}`)
        throw new HttpFailure(403, "Use the local launcher address.");
      const path = new URL(request.url ?? "/", origin).pathname;
      if (request.method === "GET" && path === "/rooms-config")
        return json(200, { url: roomOrigin });
      if (request.method === "GET" && path === "/health") {
        if (request.headers.origin === "http://127.0.0.1:3000")
          response.setHeader(
            "Access-Control-Allow-Origin",
            request.headers.origin,
          );
        return json(200, { status: "ready", mode: "local-prototype" });
      }
      if (path === "/session" || request.method === "POST") {
        if (!native && !sameOrigin)
          throw new HttpFailure(403, "Untrusted origin.");
      }
      if (request.method === "GET" && path === "/session")
        return json(200, { token });
      if (request.method === "POST") {
        const received =
          request.headers.authorization?.replace(/^Bearer /, "") ?? "";
        if (
          Buffer.byteLength(received) !== Buffer.byteLength(token) ||
          !timingSafeEqual(Buffer.from(received), Buffer.from(token))
        )
          throw new HttpFailure(401, "Restart the local game connection.");
        if (
          ![
            "/transcribe",
            "/interpret",
            "/speech",
            "/conversation",
            "/roleplay",
            "/life/review",
            "/slice/review",
          ].includes(path)
        )
          throw new HttpFailure(404, "Not found.");
        if (active >= 2)
          throw new HttpFailure(
            503,
            "Local AI is busy. Your task is kept; try again or type your reply.",
          );
        active++;
        const controller = new AbortController();
        const abort = () => controller.abort();
        request.once("aborted", abort);
        response.once("close", abort);
        try {
          const input = await body(request);
          if (path === "/transcribe") {
            let audio: Buffer;
            try {
              audio = normalizeWave(input);
            } catch {
              throw new HttpFailure(
                400,
                "Use a 16-bit PCM WAV recording of up to 20 seconds.",
              );
            }
            const text = (
              await options.transcribe(audio, controller.signal)
            ).trim();
            if (!text || text.length > 1000 || /^\[.*\]$/.test(text))
              throw new HttpFailure(
                422,
                "No clear speech found. Please try again or type your reply.",
              );
            return json(200, { text });
          }
          let value: unknown;
          try {
            value = JSON.parse(input.toString("utf8"));
          } catch {
            throw new HttpFailure(400, "Invalid request.");
          }
          if (path === "/life/review") {
            const parsed = lifeInput.safeParse(value);
            if (!parsed.success)
              throw new HttpFailure(
                400,
                "Choose a current Life task and use a short reply.",
              );
            if (!options.lifeReview)
              throw new HttpFailure(
                503,
                "Restart the companion to enable Life practice.",
              );
            const checked = lifeOutput.safeParse(
              await options.lifeReview(parsed.data, controller.signal),
            );
            if (!checked.success)
              throw new HttpFailure(
                502,
                "That reply could not be checked. Your task is kept; please retry.",
              );
            return json(200, checked.data);
          }
          if (path === "/slice/review") {
            const parsed = sliceInput.safeParse(value);
            if (!parsed.success)
              throw new HttpFailure(
                400,
                "Use a short reply and a known IELTS band.",
              );
            if (!options.sliceReview)
              throw new HttpFailure(
                503,
                "Restart the companion to enable Forest Café feedback.",
              );
            const checked = sliceOutput.safeParse(
              await options.sliceReview(parsed.data, controller.signal),
            );
            if (!checked.success)
              throw new HttpFailure(
                502,
                "Feedback is unavailable for that reply. Your conversation is kept.",
              );
            return json(200, checked.data);
          }
          if (path === "/interpret") {
            const parsed = replySchema.safeParse(value);
            if (!parsed.success)
              throw new HttpFailure(
                400,
                "Use a short reply for the current conversation.",
              );
            const result = intentSchema.safeParse(
              await options.interpret(parsed.data, controller.signal),
            );
            if (!result.success)
              throw new HttpFailure(
                502,
                "Please clarify your order. The task has not changed.",
              );
            return json(200, result.data);
          }
          if (path === "/conversation") {
            const parsed = conversationInput.safeParse(value);
            if (!parsed.success)
              throw new HttpFailure(
                400,
                "Use a short reply for the current conversation turn.",
              );
            const checked = conversationOutput.safeParse(
              await options.converse(parsed.data, controller.signal),
            );
            if (!checked.success)
              throw new HttpFailure(
                502,
                "Alex could not check that reply. Your conversation is kept; please retry.",
              );
            return json(200, checked.data);
          }
          if (path === "/roleplay") {
            const parsed = roleplayInput.safeParse(value);
            if (!parsed.success)
              throw new HttpFailure(
                400,
                "Choose a scene and role, and use a short reply for the current turn.",
              );
            if (!options.roleplay)
              throw new HttpFailure(
                503,
                "Restart the local game to enable roleplay.",
              );
            const checked = roleplayOutput.safeParse(
              await options.roleplay(parsed.data, controller.signal),
            );
            if (!checked.success)
              throw new HttpFailure(
                502,
                "That reply could not be checked. Your turn is kept; please retry.",
              );
            return json(200, checked.data);
          }
          const parsed = z
            .strictObject({ text: z.string().trim().min(1).max(500) })
            .safeParse(value);
          if (!parsed.success)
            throw new HttpFailure(400, "Use a short NPC line.");
          const audio = await options.speak(
            parsed.data.text,
            controller.signal,
          );
          response.writeHead(200, {
            "Content-Type": "audio/wav",
            "Cache-Control": "no-store",
          });
          return response.end(audio);
        } finally {
          request.off("aborted", abort);
          response.off("close", abort);
          active--;
        }
      }
      if (request.method !== "GET") throw new HttpFailure(404, "Not found.");
      let file: string;
      if (path === "/download/macos" && options.macArchive) {
        file = options.macArchive;
      } else {
        const root = await realpath(options.webRoot);
        file = await realpath(
          resolve(
            root,
            "." +
              decodeURIComponent(
                path.endsWith("/") ? path + "index.html" : path,
              ),
          ),
        );
        if (!file.startsWith(root + sep) || !types[extname(file)])
          throw new HttpFailure(404, "Not found.");
      }
      // Read before committing success; a missing export must not look like an empty ZIP.
      let content: Buffer;
      try {
        content = await readFile(file);
      } catch (error) {
        if (
          path === "/download/macos" &&
          (error as NodeJS.ErrnoException).code === "ENOENT"
        )
          throw new HttpFailure(
            404,
            "Build the desktop preview first: pnpm unity:build:mac.",
          );
        throw error;
      }
      if (path === "/download/macos")
        response.setHeader(
          "Content-Disposition",
          'attachment; filename="ContextSpeak-Cafe-macOS.zip"',
        );
      response.writeHead(200, {
        "Content-Type": types[extname(file)] ?? "application/octet-stream",
      });
      response.end(content);
    } catch (error) {
      if (response.destroyed) return;
      if (response.headersSent) {
        response.end();
        return;
      }
      if (error instanceof HttpFailure)
        json(error.status, { error: error.message });
      else if ((error as NodeJS.ErrnoException).code === "ENOENT")
        json(404, { error: "Build the cafe first: pnpm world:build." });
      else
        json(503, {
          error:
            "Local AI is unavailable. Your task is kept; try again or type your reply.",
        });
    }
  });
  server.requestTimeout = 100000;
  return server;
}
