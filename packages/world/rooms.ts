import { randomBytes } from "node:crypto";
import { z } from "zod";

export const poseSchema = z
  .object({
    x: z.number().min(-70).max(70),
    y: z.number().min(-5).max(20),
    z: z.number().min(-70).max(90),
    yaw: z.number().min(-360).max(360),
    moving: z.boolean(),
    activity: z.enum(["exploring", "talking", "recording"]),
    npc: z
      .string()
      .max(40)
      .regex(/^[^<>\p{Cc}]*$/u),
  })
  .strict();
export const playerSchema = z
  .object({
    name: z
      .string()
      .trim()
      .min(1)
      .max(24)
      .regex(/^[^<>\p{Cc}]+$/u),
    look: z.enum(["Girl", "Boy"]),
    world: z.literal("ForestSlice"),
    pose: poseSchema,
  })
  .strict();
export const joinSchema = playerSchema.extend({
  code: z.string().regex(/^[A-F0-9]{12}$/),
});
export const pollSchema = playerSchema
  .pick({ name: true, look: true })
  .partial()
  .extend({ pose: poseSchema })
  .strict();
type Pose = z.infer<typeof poseSchema>;
type Player = { id: string; name: string; look: "Girl" | "Boy" } & Pose;
type Member = { player: Player; token: string; seen: number };
type Room = { code: string; members: Map<string, Member> };

export class RoomFailure extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

/** Ephemeral presence only. Room packets never contain grades, saves or recordings. */
export class FriendRooms {
  private readonly rooms = new Map<string, Room>();
  constructor(private readonly now = Date.now) {}

  private expire() {
    for (const room of this.rooms.values()) {
      for (const [id, member] of room.members)
        if (this.now() - member.seen >= 60_000) room.members.delete(id);
      if (!room.members.size) this.rooms.delete(room.code);
    }
  }

  create(input: z.infer<typeof playerSchema>) {
    this.expire();
    if (this.rooms.size >= 100)
      throw new RoomFailure(503, "Rooms are busy. Try again shortly.");
    let code: string;
    do {
      code = randomBytes(6).toString("hex").toUpperCase();
    } while (this.rooms.has(code));
    const room: Room = { code, members: new Map() };
    this.rooms.set(code, room);
    return this.add(room, input);
  }

  join(input: z.infer<typeof joinSchema>) {
    this.expire();
    const room = this.rooms.get(input.code);
    if (!room)
      throw new RoomFailure(404, "Room not found or expired. Check the code.");
    if (room.members.size >= 4)
      throw new RoomFailure(409, "This room already has four players.");
    return this.add(room, input);
  }

  private add(room: Room, input: z.infer<typeof playerSchema>) {
    const member: Member = {
      token: randomBytes(32).toString("base64url"),
      seen: this.now(),
      player: {
        id: randomBytes(12).toString("hex"),
        name: input.name,
        look: input.look,
        ...input.pose,
      },
    };
    room.members.set(member.player.id, member);
    return { ...this.snapshot(room, member), token: member.token };
  }

  private find(token: string) {
    this.expire();
    for (const room of this.rooms.values())
      for (const member of room.members.values())
        if (member.token === token) return { room, member };
    throw new RoomFailure(401, "Room connection expired. Please join again.");
  }

  private snapshot(room: Room, member: Member) {
    return {
      code: room.code,
      self: member.player.id,
      players: [...room.members.values()].map((item) => item.player),
    };
  }

  poll(
    token: string,
    pose: Pose,
    profile: { name?: string; look?: "Girl" | "Boy" } = {},
  ) {
    const { room, member } = this.find(token);
    Object.assign(member.player, profile, pose);
    member.seen = this.now();
    return this.snapshot(room, member);
  }

  leave(token: string) {
    const { room, member } = this.find(token);
    room.members.delete(member.player.id);
    if (!room.members.size) this.rooms.delete(room.code);
    return { left: true };
  }
}
