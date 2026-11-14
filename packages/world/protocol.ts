import { z } from "zod";

// One conversational action per model reply: an order cannot invent other choices.
export const intentSchema = z.discriminatedUnion("action", [
  z.strictObject({
    action: z.literal("order"),
    items: z
      .array(
        z.strictObject({
          drink: z.enum(["coffee", "latte", "tea", "lemonade"]),
          quantity: z.number().int().min(1).max(2),
        }),
      )
      .min(1)
      .max(2),
  }),
  z.strictObject({
    action: z.literal("temperature"),
    temperature: z.enum(["hot", "iced"]),
  }),
  z.strictObject({
    action: z.literal("service"),
    service: z.enum(["here", "takeaway"]),
  }),
  z.strictObject({ action: z.literal("confirm") }),
  z.strictObject({ action: z.literal("cancel") }),
  z.strictObject({ action: z.literal("ask_price") }),
  z.strictObject({ action: z.literal("repeat") }),
  z.strictObject({ action: z.literal("unknown") }),
]);

export const replySchema = z.strictObject({
  text: z.string().trim().min(1).max(1000),
  stage: z.enum(["order", "temperature", "service", "confirm"]),
});

/** Accept only bounded PCM WAV, then resample locally; no ffmpeg or cloud upload. */
export function normalizeWave(input: Buffer, maxSeconds = 20) {
  const invalid = () => {
    throw new Error("Record up to 20 seconds as 16-bit PCM WAV.");
  };
  if (
    input.length < 44 ||
    input.length > 8 * 1024 * 1024 ||
    input.toString("ascii", 0, 4) !== "RIFF" ||
    input.toString("ascii", 8, 12) !== "WAVE" ||
    input.readUInt32LE(4) + 8 !== input.length
  )
    invalid();
  let rate = 0;
  let channels = 0;
  let pcm: Buffer | undefined;
  let offset = 12;
  while (offset + 8 <= input.length) {
    const kind = input.toString("ascii", offset, offset + 4);
    const length = input.readUInt32LE(offset + 4);
    const start = offset + 8;
    if (start + length > input.length) invalid();
    if (kind === "fmt ") {
      if (
        rate ||
        length < 16 ||
        input.readUInt16LE(start) !== 1 ||
        input.readUInt16LE(start + 14) !== 16
      )
        invalid();
      channels = input.readUInt16LE(start + 2);
      rate = input.readUInt32LE(start + 4);
      if (
        ![1, 2].includes(channels) ||
        rate < 8000 ||
        rate > 48000 ||
        input.readUInt16LE(start + 12) !== channels * 2 ||
        input.readUInt32LE(start + 8) !== rate * channels * 2
      )
        invalid();
    }
    if (kind === "data") {
      if (pcm || !length) invalid();
      pcm = input.subarray(start, start + length);
    }
    offset = start + length + (length % 2);
  }
  if (!rate || !pcm || pcm.length % (channels * 2) || offset !== input.length)
    invalid();
  const source = pcm!;
  const frames = source.length / (channels * 2);
  if (
    frames / rate > Math.min(120, Math.max(1, maxSeconds)) ||
    frames < rate / 10
  )
    invalid();
  const count = Math.floor((frames * 16000) / rate);
  const output = Buffer.alloc(44 + count * 2);
  output.write("RIFF");
  output.writeUInt32LE(output.length - 8, 4);
  output.write("WAVEfmt ", 8);
  output.writeUInt32LE(16, 16);
  output.writeUInt16LE(1, 20);
  output.writeUInt16LE(1, 22);
  output.writeUInt32LE(16000, 24);
  output.writeUInt32LE(32000, 28);
  output.writeUInt16LE(2, 32);
  output.writeUInt16LE(16, 34);
  output.write("data", 36);
  output.writeUInt32LE(count * 2, 40);
  for (let index = 0; index < count; index++) {
    const position = (index * rate) / 16000;
    const left = Math.floor(position);
    const right = Math.min(frames - 1, left + 1);
    let sample = 0;
    for (let channel = 0; channel < channels; channel++) {
      const a = source.readInt16LE((left * channels + channel) * 2);
      const b = source.readInt16LE((right * channels + channel) * 2);
      sample += a + (b - a) * (position - left);
    }
    output.writeInt16LE(Math.round(sample / channels), 44 + index * 2);
  }
  return output;
}
