import { createRoomServer } from "../packages/world/room-server";

// Hosting is opt-in; never expose the local AI companion through this listener.
const host = process.env.CONTEXTSPEAK_ROOMS_HOST ?? "127.0.0.1";
const port = Number(process.env.CONTEXTSPEAK_ROOMS_PORT ?? 4330);
const origins = (
  process.env.CONTEXTSPEAK_ROOM_ORIGINS ?? "http://127.0.0.1:4320"
)
  .split(",")
  .map((value) => new URL(value.trim()).origin);
const server = createRoomServer({ origins });
server.listen(port, host, () =>
  console.log(`ContextSpeak friend rooms: http://${host}:${port}`),
);
server.on("error", () => {
  console.error("Could not start friend rooms. Check the host/port.");
  process.exitCode = 1;
});
for (const signal of ["SIGINT", "SIGTERM"] as const)
  process.once(signal, () => server.close(() => process.exit(0)));
