import { afterEach, expect, it } from "vitest";
import request from "supertest";
import { createRoomServer } from "./room-server";

const servers: ReturnType<typeof createRoomServer>[] = [];
afterEach(() => servers.splice(0).forEach((server) => server.close()));
function setup(now = () => Date.now()) {
  const server = createRoomServer({ now });
  servers.push(server);
  const api = request(server);
  const post = (path: string, data: object, token?: string) => {
    const call = api
      .post(`/rooms/${path}`)
      .set("X-ContextSpeak-Client", "native");
    if (token) call.auth(token, { type: "bearer" });
    return call.send(data);
  };
  return { api, post };
}
const pose = {
  x: 0,
  y: 0,
  z: 0,
  yaw: 0,
  moving: false,
  activity: "exploring",
  npc: "",
};
const player = (name = "Alex") => ({
  name,
  look: "Girl",
  world: "ForestSlice",
  pose,
});

it("creates private rooms and syncs only public presence", async () => {
  const { post } = setup();
  const host = (await post("create", player())).body;
  expect(host.code).toMatch(/^[A-F0-9]{12}$/);
  const guest = (await post("join", { ...player("Maya"), code: host.code }))
    .body;
  expect(guest.self).not.toBe(host.self);
  expect(guest.players).toHaveLength(2);
  const changed = {
    ...pose,
    x: 12,
    z: 7,
    moving: true,
    activity: "talking",
    npc: "Rowan",
  };
  await post(
    "poll",
    { pose: changed, look: "Boy", name: "Lucas" },
    guest.token,
  ).expect(200);
  const state = (await post("poll", { pose }, host.token)).body;
  expect(
    state.players.find((p: { id: string }) => p.id === guest.self),
  ).toMatchObject({ ...changed, look: "Boy", name: "Lucas" });
  expect(JSON.stringify(state)).not.toContain(guest.token);
  expect(state.players[0]).not.toHaveProperty("wallet");
});

it("requires the participant token and isolates rooms", async () => {
  const { post } = setup();
  const a = (await post("create", player())).body;
  const b = (await post("create", player("Maya"))).body;
  await post("poll", { pose }).expect(401);
  await post("poll", { pose }, a.code).expect(401);
  expect((await post("poll", { pose }, b.token)).body.players).toHaveLength(1);
  await post("leave", {}, a.token).expect(200);
  await post("poll", { pose }, a.token).expect(401);
  expect((await post("poll", { pose }, b.token)).body.players).toHaveLength(1);
});

it("limits rooms to four and frees a slot after leave", async () => {
  const { post } = setup();
  const host = (await post("create", player())).body;
  const join = () => post("join", { ...player("Friend"), code: host.code });
  const guest = (await join()).body;
  await join().expect(200);
  await join().expect(200);
  await join().expect(409);
  await post("leave", {}, guest.token).expect(200);
  await join().expect(200);
});

it("expires abandoned players, and deletes empty rooms", async () => {
  let time = 0;
  const { post } = setup(() => time);
  const host = (await post("create", player())).body;
  const guest = (await post("join", { ...player("Maya"), code: host.code }))
    .body;
  time = 40_000;
  await post("poll", { pose }, host.token).expect(200);
  time = 65_000;
  expect((await post("poll", { pose }, host.token)).body.players).toHaveLength(
    1,
  );
  await post("poll", { pose }, guest.token).expect(401);
  time = 130_000;
  await post("join", { ...player(), code: host.code }).expect(404);
});

it("rejects invalid poses and game-state mutations without changing presence", async () => {
  const { post } = setup();
  const host = (await post("create", player())).body;
  for (const bad of [
    { ...pose, x: 999 },
    { ...pose, x: "12" },
    { ...pose, wallet: 999 },
  ])
    await post("poll", { pose: bad }, host.token).expect(400);
  await post("create", { ...player(), world: "OtherWorld" }).expect(400);
  await post("create", player("<script>bad</script>")).expect(400);
  await post("join", { ...player(), code: "BAD" }).expect(400);
  expect((await post("poll", { pose }, host.token)).body.players[0].x).toBe(0);
});

it("enforces browser origins and JSON/body limits", async () => {
  const { api } = setup();
  await api
    .post("/rooms/create")
    .set("Origin", "https://attacker.example")
    .send(player())
    .expect(403);
  await api.post("/rooms/create").send(player()).expect(403);
  const allowed = await api
    .options("/rooms/create")
    .set("Origin", "http://127.0.0.1:4320")
    .expect(204);
  expect(allowed.headers["access-control-allow-origin"]).toBe(
    "http://127.0.0.1:4320",
  );
  await api
    .post("/rooms/create")
    .set("Origin", "http://127.0.0.1:4320")
    .send(player())
    .expect(200);
  await api
    .post("/rooms/create")
    .set("X-ContextSpeak-Client", "native")
    .send({ text: "x".repeat(5000) })
    .expect(413);
});
