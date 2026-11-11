import { describe, expect, it } from "vitest";
import { intentSchema, normalizeWave } from "./protocol";

function wave(rate = 24000, channels = 2, seconds = 1) {
  const bytes = rate * channels * seconds * 2;
  const result = Buffer.alloc(44 + bytes);
  result.write("RIFF");
  result.writeUInt32LE(result.length - 8, 4);
  result.write("WAVEfmt ", 8);
  result.writeUInt32LE(16, 16);
  result.writeUInt16LE(1, 20);
  result.writeUInt16LE(channels, 22);
  result.writeUInt32LE(rate, 24);
  result.writeUInt32LE(rate * channels * 2, 28);
  result.writeUInt16LE(channels * 2, 32);
  result.writeUInt16LE(16, 34);
  result.write("data", 36);
  result.writeUInt32LE(bytes, 40);
  return result;
}

describe("local cafe protocol", () => {
  it("converts recorded stereo PCM to bounded 16 kHz mono for Whisper", () => {
    const result = normalizeWave(wave());
    expect(result.readUInt32LE(24)).toBe(16000);
    expect(result.readUInt16LE(22)).toBe(1);
    expect(result.length).toBe(32044);
  });
  it("rejects malformed, compressed or excessive recordings", () => {
    expect(() => normalizeWave(Buffer.from("not a recording"))).toThrow();
    const compressed = wave();
    compressed.writeUInt16LE(3, 20);
    expect(() => normalizeWave(compressed)).toThrow();
    expect(() => normalizeWave(wave(16000, 1, 61))).toThrow();
    const truncated = wave().subarray(0, 48);
    expect(() => normalizeWave(truncated)).toThrow();
  });
  it("models can describe an order but cannot grant rewards or invent prices", () => {
    expect(
      intentSchema.safeParse({ action: "complete", reward: 100 }).success,
    ).toBe(false);
    expect(
      intentSchema.safeParse({
        action: "order",
        items: [{ drink: "coffee", quantity: -2 }],
      }).success,
    ).toBe(false);
    expect(
      intentSchema.safeParse({
        action: "order",
        items: [{ drink: "coffee", quantity: 2, price: 0 }],
      }).success,
    ).toBe(false);
    expect(
      intentSchema.safeParse({
        action: "order",
        items: [{ drink: "coffee", quantity: 2 }],
        temperature: "hot",
        service: "here",
      }).success,
    ).toBe(false);
    expect(
      intentSchema.parse({
        action: "order",
        items: [{ drink: "tea", quantity: 2 }],
      }),
    ).toEqual({ action: "order", items: [{ drink: "tea", quantity: 2 }] });
  });
});
