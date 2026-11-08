import { afterEach, expect, it, vi } from "vitest";
import { AudioPlayer } from "./player";
afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});
const wave = {
  audio: Buffer.from("test audio").toString("base64"),
  mimeType: "audio/wav" as const,
};
it("cancels_previous_audio_and_releases_the_matching_recording_only", async () => {
  let pauses = 0;
  vi.stubGlobal(
    "Audio",
    class {
      src = "";
      pause() {
        pauses++;
      }
      removeAttribute() {}
      load() {}
      async play() {}
    },
  );
  const release = vi.spyOn(URL, "revokeObjectURL");
  const player = new AudioPlayer();
  const first = player.play("one", wave);
  const second = player.play("two", wave);
  expect(await first).toEqual({ cancelled: true });
  player.stop("one");
  expect(pauses).toBe(1);
  player.stop("two");
  expect(await second).toEqual({ cancelled: true });
  expect(release).toHaveBeenCalledTimes(2);
});
it("releases_audio_even_when_the_browser_throws_before_starting_playback", async () => {
  vi.stubGlobal(
    "Audio",
    class {
      src = "";
      pause() {}
      removeAttribute() {}
      load() {}
      play() {
        throw new Error("blocked");
      }
    },
  );
  const release = vi.spyOn(URL, "revokeObjectURL");
  const player = new AudioPlayer();
  await expect(player.play("one", wave)).rejects.toBeInstanceOf(Error);
  try {
    expect(release).toHaveBeenCalledTimes(1);
  } finally {
    player.stop();
  }
});
