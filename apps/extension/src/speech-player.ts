import { browser } from "wxt/browser";
import type { SpeechAudio } from "@contextspeak/contracts";
let creating: Promise<void> | undefined;
async function contexts() {
  return browser.runtime.getContexts({
    contextTypes: ["OFFSCREEN_DOCUMENT"],
    documentUrls: [chrome.runtime.getURL("audio.html")],
  });
}
async function ensurePlayer() {
  if ((await contexts()).length) return;
  creating ??= browser.offscreen
    .createDocument({
      url: "audio.html",
      reasons: ["AUDIO_PLAYBACK"],
      justification:
        "Play requested AI pronunciation without webpage media restrictions.",
    })
    .finally(() => {
      creating = undefined;
    });
  await creating;
}
export async function playSpeech(
  id: string,
  audio: SpeechAudio,
  signal: AbortSignal,
) {
  await ensurePlayer();
  if (signal.aborted) return { ok: true, cancelled: true };
  return browser.runtime.sendMessage({
    type: "speech-player",
    command: "play",
    requestId: id,
    audio,
  });
}
export async function stopSpeech(id: string) {
  if (!(await contexts()).length) return;
  await browser.runtime.sendMessage({
    type: "speech-player",
    command: "stop",
    requestId: id,
  });
}
