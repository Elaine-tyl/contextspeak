export type GameCue = "select" | "success" | "retry" | "complete";
const notes: Record<GameCue, number[]> = {
  select: [440],
  success: [523, 659, 784],
  retry: [330, 294],
  complete: [523, 659, 784, 1047],
};

/** Short original synthesized effects; no downloads, autoplay or shared lesson audio. */
export class GameAudio {
  private quiet = false;
  private voices = new Set<OscillatorNode>();
  get muted() {
    return this.quiet;
  }
  set muted(value: boolean) {
    this.quiet = value;
    if (value) this.stop();
  }
  private context?: AudioContext;
  private disposed = false;
  constructor(private factory = () => new AudioContext()) {}

  async unlock() {
    if (this.disposed || this.muted) return;
    try {
      this.context ??= this.factory();
      if (this.context.state === "suspended") await this.context.resume();
    } catch {
      // Sound is optional: a blocked audio device must never prevent an answer.
    }
  }

  play(cue: GameCue) {
    const context = this.context;
    if (this.disposed || this.muted || context?.state !== "running") return;
    try {
      notes[cue].forEach((frequency, index) => {
        const tone = context.createOscillator();
        const gain = context.createGain();
        const start = context.currentTime + index * 0.09;
        tone.type = "sine";
        tone.frequency.setValueAtTime(frequency, start);
        gain.gain.setValueAtTime(0.0001, start);
        gain.gain.exponentialRampToValueAtTime(0.07, start + 0.012);
        gain.gain.exponentialRampToValueAtTime(0.0001, start + 0.16);
        tone.connect(gain);
        gain.connect(context.destination);
        this.voices.add(tone);
        tone.onended = () => {
          this.voices.delete(tone);
          tone.disconnect();
          gain.disconnect();
        };
        tone.start(start);
        tone.stop(start + 0.18);
      });
    } catch {
      /* Feedback remains visible when a device disconnects. */
    }
  }

  dispose() {
    this.disposed = true;
    this.stop();
    void this.context?.close().catch(() => {});
  }

  private stop() {
    for (const tone of this.voices) {
      try {
        tone.stop();
      } catch {
        /* Already-ended tones need no cleanup. */
      }
    }
    this.voices.clear();
  }
}
