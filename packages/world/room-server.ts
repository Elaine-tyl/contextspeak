import { createServer, type IncomingMessage } from "node:http";
import {
  FriendRooms,
  RoomFailure,
  playerSchema,
  joinSchema,
  pollSchema,
} from "./rooms";

interface RoomOptions {
  now?: () => number;
  origins?: string[];
}
async function readJson(request: IncomingMessage) {
  if (!request.headers["content-type"]?.startsWith("application/json"))
    throw new RoomFailure(415, "Use JSON.");
  if (Number(request.headers["content-length"] ?? 0) > 4096) {
    request.resume();
    throw new RoomFailure(413, "Room request is too large.");
  }
  let size = 0;
  const chunks: Buffer[] = [];
  for await (const chunk of request) {
    size += chunk.length;
    if (size > 4096) throw new RoomFailure(413, "Room request is too large.");
    chunks.push(chunk);
  }
  try {
    return JSON.parse(Buffer.concat(chunks).toString("utf8"));
  } catch {
    throw new RoomFailure(400, "Invalid JSON.");
  }
}

/** Separate from local AI: no speech, account, filesystem or inference routes. */
export function createRoomServer(options: RoomOptions = {}) {
  const rooms = new FriendRooms(options.now);
  const origins = new Set(options.origins ?? ["http://127.0.0.1:4320"]);
  return createServer(
    { requestTimeout: 5000, headersTimeout: 5000, maxHeaderSize: 8192 },
    async (request, response) => {
      const json = (status: number, value: unknown) => {
        response.writeHead(status, {
          "Content-Type": "application/json",
          "Cache-Control": "no-store",
          "X-Content-Type-Options": "nosniff",
        });
        response.end(JSON.stringify(value));
      };
      try {
        if (request.method === "GET" && request.url === "/health")
          return json(200, { status: "ready", mode: "friend-rooms" });
        const origin = request.headers.origin;
        if (
          origin
            ? !origins.has(origin)
            : request.headers["x-contextspeak-client"] !== "native"
        )
          throw new RoomFailure(403, "Untrusted origin.");
        if (origin) {
          response.setHeader("Access-Control-Allow-Origin", origin);
          response.setHeader("Vary", "Origin");
        }
        if (request.method === "OPTIONS") {
          response.writeHead(204, {
            "Access-Control-Allow-Methods": "POST, OPTIONS",
            "Access-Control-Allow-Headers":
              "Content-Type, Authorization, X-ContextSpeak-Client",
            "Access-Control-Max-Age": "600",
          });
          return response.end();
        }
        if (
          request.method !== "POST" ||
          ![
            "/rooms/create",
            "/rooms/join",
            "/rooms/poll",
            "/rooms/leave",
          ].includes(request.url ?? "")
        )
          throw new RoomFailure(404, "Not found.");
        const value = await readJson(request);
        let result: unknown;
        if (request.url === "/rooms/create")
          result = rooms.create(playerSchema.parse(value));
        else if (request.url === "/rooms/join")
          result = rooms.join(joinSchema.parse(value));
        else {
          const token =
            request.headers.authorization?.match(
              /^Bearer ([A-Za-z0-9_-]{43})$/,
            )?.[1] ?? "";
          if (request.url === "/rooms/poll") {
            const { pose, ...profile } = pollSchema.parse(value);
            result = rooms.poll(token, pose, profile);
          } else result = rooms.leave(token);
        }
        json(200, result);
      } catch (error) {
        const failure =
          error instanceof RoomFailure
            ? error
            : new RoomFailure(
                400,
                "Use a valid room code, name and player position.",
              );
        json(failure.status, { error: failure.message });
      }
    },
  );
}
