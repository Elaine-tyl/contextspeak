import type { SpeechAudio } from "@contextspeak/contracts";
export class AudioPlayer {
  private current:
    | { id: string; finish(cancelled?: boolean, error?: Error): void }
    | undefined;
  stop(id?: string) {
    if (!id || this.current?.id === id) this.current?.finish(true);
  }
  async play(id: string, value: SpeechAudio): Promise<{ cancelled: boolean }> {
    this.stop();
    const bytes = Uint8Array.from(atob(value.audio), (character) =>
      character.charCodeAt(0),
    );
    const url = URL.createObjectURL(
      new Blob([bytes], { type: value.mimeType }),
    );
    const audio = new Audio();
    audio.src = url;
    return new Promise((resolve, reject) => {
      let deadline: ReturnType<typeof setTimeout>;
      const current = {
        id,
        finish: (cancelled = false, error?: Error) => {
          if (this.current !== current) return;
          this.current = undefined;
          clearTimeout(deadline);
          audio.onended = audio.onerror = null;
          audio.pause();
          audio.removeAttribute("src");
          audio.load();
          URL.revokeObjectURL(url);
          if (error) reject(error);
          else resolve({ cancelled });
        },
      };
      this.current = current;
      audio.onended = () => current.finish();
      audio.onerror = () =>
        current.finish(false, new Error("AI audio could not play. Try again."));
      deadline = setTimeout(
        () =>
          current.finish(false, new Error("AI audio timed out. Try again.")),
        120000,
      );
      const failed = () =>
        current.finish(
          false,
          new Error("AI audio could not start. Try again."),
        );
      try {
        audio.play().catch(failed);
      } catch {
        failed();
      }
    });
  }
}
