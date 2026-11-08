import { browser } from "wxt/browser";
import { speechAudioSchema } from "@contextspeak/contracts";
import { AudioPlayer } from "../../../../packages/audio/player";
const player = new AudioPlayer();
const cancelled: string[] = [];
browser.runtime.onMessage.addListener((message, sender) => {
  if (
    message?.type !== "speech-player" ||
    sender.id !== browser.runtime.id ||
    sender.tab
  )
    return;
  return (async () => {
    const id = message.requestId;
    if (typeof id !== "string") return { ok: false };
    if (message.command === "stop") {
      cancelled.push(id);
      if (cancelled.length > 100) cancelled.shift();
      player.stop(id);
      return { ok: true };
    }
    if (message.command !== "play" || cancelled.includes(id))
      return { ok: true, cancelled: true };
    try {
      return {
        ok: true,
        ...(await player.play(id, speechAudioSchema.parse(message.audio))),
      };
    } catch {
      return { ok: false, error: "AI audio could not play. Try again." };
    }
  })();
});
window.addEventListener("pagehide", () => player.stop());
