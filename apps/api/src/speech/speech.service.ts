import { HttpException } from "@nestjs/common";
import { createHash } from "node:crypto";
import type {
  Identity,
  SpeechAudio,
  SpeechInput,
} from "@contextspeak/contracts";
import type { LibraryService } from "../library/library.service";
import type { SpeechProvider } from "./gemini-speech.provider";
import type { SpeechStore } from "./speech.store";
const requestKey = (userId: string, input: SpeechInput) =>
  createHash("sha256")
    .update(JSON.stringify([userId, input]))
    .digest("hex");
type SpeechReply = SpeechAudio & { cached: boolean };
export class SpeechService {
  private cache = new Map<
    string,
    { userId: string; audio: SpeechAudio; expires: number }
  >();
  private pending = new Map<
    string,
    { userId: string; token: symbol; work: Promise<SpeechReply> }
  >();
  private bytes = 0;
  private requests: number[] = [];
  private active = 0;
  private localTail = Promise.resolve();
  private localCount = 0;
  private cleanupTimer?: ReturnType<typeof setInterval>;
  constructor(
    private library: LibraryService,
    private provider?: SpeechProvider,
    private store?: SpeechStore,
    private fallback?: SpeechProvider,
    private retain: (
      userId: string,
      keep: () => void,
    ) => Promise<void> = async (_userId, keep) => {
      keep();
    },
  ) {}
  forgetUser(userId: string) {
    for (const [key, entry] of this.cache)
      if (entry.userId === userId) this.remove(key);
    for (const [key, entry] of this.pending)
      if (entry.userId === userId) this.pending.delete(key);
  }
  async onModuleInit() {
    if (!this.store) return;
    await this.store.cleanup();
    this.cleanupTimer = setInterval(
      () =>
        void this.store!.cleanup().catch(() =>
          console.warn("Speech storage cleanup failed."),
        ),
      300000,
    );
    this.cleanupTimer.unref();
  }
  onModuleDestroy() {
    clearInterval(this.cleanupTimer);
  }
  async speak(identity: Identity, input: SpeechInput) {
    const user = await this.library.actor(identity);
    const provider =
      this.provider?.languages &&
      !this.provider.languages.includes(input.language)
        ? this.fallback
        : this.provider;
    const key = createHash("sha256")
      .update(JSON.stringify([user.id, provider?.cacheKey ?? "default", input]))
      .digest("hex");
    const now = Date.now();
    for (const [id, entry] of this.cache)
      if (entry.expires <= now) this.remove(id);
    const cached = this.cache.get(key);
    if (cached) {
      this.cache.delete(key);
      this.cache.set(key, cached);
      return { ...cached.audio, cached: true };
    }
    const pending = this.pending.get(key);
    if (pending) return { ...(await pending.work), cached: true };
    // Reserve before the asynchronous disk lookup so restart misses also coalesce.
    const token = Symbol();
    const generation = this.resolve(user.id, key, input, token, provider);
    this.pending.set(key, { userId: user.id, token, work: generation });
    try {
      return await generation;
    } finally {
      if (this.pending.get(key)?.work === generation) this.pending.delete(key);
    }
  }
  private async resolve(
    userId: string,
    key: string,
    input: SpeechInput,
    token: symbol,
    provider?: SpeechProvider,
  ): Promise<SpeechReply> {
    const stored = provider
      ? await this.store?.get(userId, key)
      : await this.store?.getLatest(userId, requestKey(userId, input));
    if (stored) {
      await this.remember(key, userId, stored, token);
      return { ...stored, cached: true };
    }
    if (!provider) throw new HttpException({ code: "SPEECH_UNAVAILABLE" }, 503);
    if (provider.local)
      return this.queueLocal(() =>
        this.generate(userId, key, input, provider, token),
      );
    const now = Date.now();
    this.requests = this.requests.filter((time) => time > now - 60000);
    if (this.active >= 2 || this.requests.length >= 6)
      throw new HttpException({ code: "SPEECH_BUSY" }, 429);
    this.requests.push(now);
    this.active++;
    try {
      return await this.generate(userId, key, input, provider, token);
    } finally {
      this.active--;
    }
  }
  private async queueLocal(work: () => Promise<SpeechReply>) {
    if (this.localCount >= 2)
      throw new HttpException({ code: "SPEECH_BUSY" }, 429);
    const previous = this.localTail;
    let release!: () => void;
    this.localTail = new Promise<void>((resolve) => {
      release = resolve;
    });
    this.localCount++;
    await previous;
    try {
      return await work();
    } finally {
      this.localCount--;
      release();
    }
  }
  private remove(key: string) {
    const entry = this.cache.get(key);
    if (entry) this.bytes -= entry.audio.audio.length;
    this.cache.delete(key);
  }
  private async remember(
    key: string,
    userId: string,
    audio: SpeechAudio,
    token: symbol,
  ) {
    await this.retain(userId, () => {
      if (this.pending.get(key)?.token !== token) return;
      this.remove(key);
      while (
        this.cache.size >= 100 ||
        this.bytes + audio.audio.length > 32 * 1024 * 1024
      ) {
        const oldest = this.cache.keys().next().value;
        if (oldest === undefined) break;
        this.remove(oldest);
      }
      if (audio.audio.length <= 32 * 1024 * 1024) {
        this.cache.set(key, { userId, audio, expires: Date.now() + 86400000 });
        this.bytes += audio.audio.length;
      }
    });
  }
  private async generate(
    userId: string,
    key: string,
    input: SpeechInput,
    provider: SpeechProvider,
    token: symbol,
  ): Promise<SpeechReply> {
    try {
      const audio = await provider.generate(input);
      await this.store?.put(userId, key, audio, requestKey(userId, input));
      await this.remember(key, userId, audio, token);
      return { ...audio, cached: false };
    } catch (error) {
      throw new HttpException(
        {
          code:
            (error as { status?: number }).status === 429
              ? "SPEECH_QUOTA"
              : "SPEECH_UNAVAILABLE",
        },
        (error as { status?: number }).status === 429 ? 429 : 503,
      );
    }
  }
}
