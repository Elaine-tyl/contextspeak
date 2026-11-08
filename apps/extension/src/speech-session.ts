import type { SpeechAudio, SpeechInput } from "@contextspeak/contracts";
interface Dependencies {
  session(): Promise<{ identity: string; token: string }>;
  generate(
    input: SpeechInput,
    token: string,
    signal: AbortSignal,
  ): Promise<SpeechAudio>;
  play(
    id: string,
    audio: SpeechAudio,
    signal: AbortSignal,
  ): Promise<{ ok: boolean; error?: string; cancelled?: boolean }>;
  stop(id: string): Promise<void>;
}
export class ExtensionSpeech {
  private current:
    { owner: string; id: string; controller: AbortController } | undefined;
  constructor(private deps: Dependencies) {}
  stop(owner?: string, id?: string) {
    const current = this.current;
    if (!current || (owner && (current.owner !== owner || current.id !== id)))
      return;
    this.current = undefined;
    current.controller.abort();
    void this.deps.stop(current.id).catch(() => {});
  }
  stopTab(tabId: number) {
    if (this.current?.owner.startsWith(`${tabId}:`)) this.stop();
  }
  async start(owner: string, id: string, input: SpeechInput) {
    this.stop();
    const current = { owner, id, controller: new AbortController() };
    this.current = current;
    const cancelled = { ok: true, cancelled: true };
    try {
      const session = await this.deps.session();
      if (this.current !== current) return cancelled;
      const audio = await this.deps.generate(
        input,
        session.token,
        current.controller.signal,
      );
      const latest = await this.deps.session();
      if (
        this.current !== current ||
        latest.identity !== session.identity ||
        latest.token !== session.token
      )
        return cancelled;
      return await this.deps.play(id, audio, current.controller.signal);
    } catch (error) {
      if (current.controller.signal.aborted) return cancelled;
      return {
        ok: false,
        error:
          (error as { status?: number }).status === 429
            ? "AI voice quota reached or busy. Try again shortly; cached audio remains available."
            : "AI voice is unavailable. Check your connection and pairing, then retry.",
      };
    } finally {
      if (this.current === current) this.current = undefined;
    }
  }
}
