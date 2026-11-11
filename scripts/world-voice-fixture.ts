// Public synthetic speech for the optional local microphone integration test.
import { mkdir, writeFile } from "node:fs/promises";
import { resolve } from "node:path";
import { localProviders } from "../packages/world/providers";
import { normalizeWave } from "../packages/world/protocol";
const providers = localProviders(resolve(".local/world-tools"));

await mkdir(".local", { recursive: true });
for (const [name, text] of Object.entries({
  order: "Two coffees, please.",
  temperature: "Hot, please.",
  service: "To go, please.",
  confirm: "Yes, please.",
  friend: "I like tea because it tastes good.",
})) {
  const spoken = normalizeWave(await providers.speak(text));
  // Give the microphone time to initialize before the phrase; avoid looping mid-reply.
  const audio = Buffer.concat([
    spoken.subarray(0, 44),
    Buffer.alloc(32000),
    spoken.subarray(44),
    Buffer.alloc(32000 * 4),
  ]);
  audio.writeUInt32LE(audio.length - 8, 4);
  audio.writeUInt32LE(audio.length - 44, 40);
  await writeFile(`.local/world-test-${name}.wav`, audio, { mode: 0o600 });
  if (name === "order")
    await writeFile(".local/world-test-audio.wav", audio, { mode: 0o600 });
}
