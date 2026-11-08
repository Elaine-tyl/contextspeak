import { HttpException } from "@nestjs/common";
import { createHash } from "node:crypto";
import type {
  Identity,
  SpeechAudio,
  SpeechInput,
} from "@contextspeak/contracts";
import type { LibraryService } from "../library/library.service";
import type { SpeechProvider } from "./gemini-speech.provider";
export class SpeechService {
  private cache = new Map<string, { audio: SpeechAudio; expires: number }>();
  private pending = new Map<string, Promise<SpeechAudio>>();
  private bytes = 0;
  private requests: number[] = [];
  constructor(
    private library: LibraryService,
    private provider?: SpeechProvider,
  ) {}
  async speak(identity: Identity, input: SpeechInput) {
    const user = await this.library.actor(identity);
    const key = createHash("sha256")
      .update(JSON.stringify([user.id, input]))
      .digest("hex");
    const now = Date.now();
    for (const [id, entry] of this.cache)
      if (entry.expires <= now) this.remove(id);
    const cached = this.cache.get(key);
    // Check successful audio before provider limits: saved playback survives quota errors.
    if (cached) {
      this.cache.delete(key);
      this.cache.set(key, cached);
      return { ...cached.audio, cached: true };
    }
    const pending = this.pending.get(key);
    if (pending) return { ...(await pending), cached: true };
    if (!this.provider)
      throw new HttpException({ code: "SPEECH_UNAVAILABLE" }, 503);
    this.requests = this.requests.filter((time) => time > now - 60000);
    if (this.pending.size >= 2 || this.requests.length >= 6)
      throw new HttpException({ code: "SPEECH_BUSY" }, 429);
    this.requests.push(now);
    const generation = this.generate(key, input);
    this.pending.set(key, generation);
    try {
      return { ...(await generation), cached: false };
    } finally {
      this.pending.delete(key);
    }
  }
  private remove(key: string) {
    const entry = this.cache.get(key);
    if (entry) this.bytes -= entry.audio.audio.length;
    this.cache.delete(key);
  }
  private async generate(key: string, input: SpeechInput) {
    try {
      const audio = await this.provider!.generate(input);
      while (
        this.cache.size >= 100 ||
        this.bytes + audio.audio.length > 32 * 1024 * 1024
      ) {
        const oldest = this.cache.keys().next().value;
        if (oldest === undefined) break;
        this.remove(oldest);
      }
      if (audio.audio.length <= 32 * 1024 * 1024) {
        this.cache.set(key, { audio, expires: Date.now() + 86400000 });
        this.bytes += audio.audio.length;
      }
      return audio;
    } catch (error) {
      const status = (error as { status?: number }).status;
      throw new HttpException(
        { code: status === 429 ? "SPEECH_QUOTA" : "SPEECH_UNAVAILABLE" },
        status === 429 ? 429 : 503,
      );
    }
  }
}
