import type { SpeechAudio, SpeechInput } from "@contextspeak/contracts";
import {
  SpeechFailure,
  validateWav,
  type SpeechProvider,
} from "./gemini-speech.provider";
export const LOCAL_SPEECH_MODEL = "phonon:31m-en";
export const LOCAL_SPEECH_DIGEST =
  "6667dd188d2e30e1448b0476ccbd36bdc8802225a848541d5049f450e2a983e0";
export class OllamaSpeechProvider implements SpeechProvider {
  readonly local = true;
  readonly languages = ["en"];
  readonly cacheKey = `ollama:${LOCAL_SPEECH_MODEL}:${LOCAL_SPEECH_DIGEST}:default:pcm-v1`;
  private url: string;
  constructor(private options: { url?: string; fetcher?: typeof fetch } = {}) {
    const url = new URL(options.url ?? "http://127.0.0.1:11434");
    if (
      url.protocol !== "http:" ||
      !["127.0.0.1", "localhost", "[::1]"].includes(url.hostname) ||
      url.username ||
      url.password ||
      url.pathname !== "/" ||
      url.search ||
      url.hash
    )
      throw new Error("Local speech requires a loopback Ollama URL.");
    this.url = url.origin;
  }
  async generate(input: SpeechInput): Promise<SpeechAudio> {
    if (input.language !== "en") throw new SpeechFailure(422);
    const signal = AbortSignal.timeout(75000);
    const fetcher = this.options.fetcher ?? fetch;
    try {
      const tags = await fetcher(this.url + "/api/tags", {
        signal,
        redirect: "error",
      });
      const models = tags.ok ? await tags.json() : null;
      if (
        !models?.models?.some(
          (model: { name: string; digest: string }) =>
            model.name === LOCAL_SPEECH_MODEL &&
            model.digest === LOCAL_SPEECH_DIGEST,
        )
      )
        throw new SpeechFailure(503);
      const response = await fetcher(this.url + "/v1/audio/speech", {
        method: "POST",
        redirect: "error",
        signal,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          model: LOCAL_SPEECH_MODEL,
          input: input.text,
          voice: "default",
          response_format: "pcm",
        }),
      });
      if (!response.ok)
        throw new SpeechFailure(response.status === 429 ? 429 : 503);
      const reader = response.body?.getReader();
      if (!reader) throw new SpeechFailure(502);
      const chunks: Uint8Array[] = [];
      let length = 0;
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        length += value.byteLength;
        if (length > 3100000 - 44) {
          await reader.cancel();
          throw new SpeechFailure(502);
        }
        chunks.push(value);
      }
      if (!length || length % 2) throw new SpeechFailure(502);
      // Ollama PCM is signed 16-bit mono at 24 kHz. Add exact WAV lengths after streaming.
      const audio = Buffer.alloc(length + 44);
      audio.write("RIFF");
      audio.writeUInt32LE(length + 36, 4);
      audio.write("WAVEfmt ", 8);
      audio.writeUInt32LE(16, 16);
      audio.writeUInt16LE(1, 20);
      audio.writeUInt16LE(1, 22);
      audio.writeUInt32LE(24000, 24);
      audio.writeUInt32LE(48000, 28);
      audio.writeUInt16LE(2, 32);
      audio.writeUInt16LE(16, 34);
      audio.write("data", 36);
      audio.writeUInt32LE(length, 40);
      Buffer.concat(chunks).copy(audio, 44);
      validateWav(audio);
      return { audio: audio.toString("base64"), mimeType: "audio/wav" };
    } catch (error) {
      if (error instanceof SpeechFailure) throw error;
      throw new SpeechFailure(signal.aborted ? 504 : 502);
    }
  }
}
