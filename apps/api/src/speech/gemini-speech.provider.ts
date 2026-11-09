import type { SpeechAudio, SpeechInput } from "@contextspeak/contracts";
export const SPEECH_MODEL = "gemini-3.8-flash-lite-tts";
export interface SpeechProvider {
  readonly cacheKey?: string;
  readonly local?: boolean;
  readonly languages?: readonly string[];
  generate(input: SpeechInput): Promise<SpeechAudio>;
}
export class SpeechFailure extends Error {
  constructor(readonly status: number) {
    super("Speech unavailable");
  }
}
export function validateWav(audio: Buffer) {
  const invalid = () => {
    throw new SpeechFailure(502);
  };
  if (
    audio.length < 44 ||
    audio.length > 3100000 ||
    audio.toString("ascii", 0, 4) !== "RIFF" ||
    audio.toString("ascii", 8, 12) !== "WAVE" ||
    audio.readUInt32LE(4) + 8 !== audio.length
  )
    invalid();
  let format = false;
  let data = false;
  let offset = 12;
  while (offset + 8 <= audio.length) {
    const kind = audio.toString("ascii", offset, offset + 4);
    const size = audio.readUInt32LE(offset + 4);
    const start = offset + 8;
    if (start + size > audio.length) invalid();
    if (kind === "fmt ") {
      if (
        format ||
        size < 16 ||
        audio.readUInt16LE(start) !== 1 ||
        audio.readUInt16LE(start + 2) !== 1 ||
        audio.readUInt16LE(start + 14) !== 16 ||
        audio.readUInt16LE(start + 12) !== 2
      )
        invalid();
      const rate = audio.readUInt32LE(start + 4);
      if (
        rate < 8000 ||
        rate > 48000 ||
        audio.readUInt32LE(start + 8) !== rate * 2
      )
        invalid();
      format = true;
    }
    if (kind === "data") {
      if (data || !size || size % 2) invalid();
      data = true;
    }
    offset = start + size + (size % 2);
  }
  if (!format || !data || offset !== audio.length) invalid();
}
export class GeminiSpeechProvider implements SpeechProvider {
  readonly cacheKey = `${SPEECH_MODEL}:Kore:v1`;
  constructor(
    private options: { apiKey?: string; fetcher?: typeof fetch } = {},
  ) {}
  async generate(input: SpeechInput): Promise<SpeechAudio> {
    if (!this.options.apiKey) throw new SpeechFailure(503);
    const signal = AbortSignal.timeout(25000);
    try {
      const response = await (this.options.fetcher ?? fetch)(
        `https://generativelanguage.googleapis.com/v1beta/models/${SPEECH_MODEL}:generateContent`,
        {
          method: "POST",
          redirect: "error",
          signal,
          headers: {
            "Content-Type": "application/json",
            "x-goog-api-key": this.options.apiKey,
          },
          body: JSON.stringify({
            contents: [
              {
                role: "user",
                parts: [
                  {
                    text: input.text,
                    speech_metadata: {
                      style: `Natural, fluent and clear conversational delivery in ${input.language}. Read the transcript verbatim.`,
                    },
                  },
                ],
              },
            ],
            generationConfig: {
              responseModalities: ["AUDIO"],
              speechConfig: { voiceConfig: { voice: "Kore" } },
            },
          }),
        },
      );
      if (!response.ok)
        throw new SpeechFailure(response.status === 429 ? 429 : 503);
      const reader = response.body?.getReader();
      if (!reader) throw new SpeechFailure(502);
      const chunks: Uint8Array[] = [];
      let size = 0;
      while (true) {
        const { value, done } = await reader.read();
        if (done) break;
        size += value.byteLength;
        if (size > 4300000) {
          await reader.cancel();
          throw new SpeechFailure(502);
        }
        chunks.push(value);
      }
      const result = JSON.parse(Buffer.concat(chunks).toString("utf8"));
      const candidate = result.candidates?.[0];
      const part = candidate?.content?.parts?.find(
        (value: any) => value.inlineData,
      )?.inlineData;
      if (
        candidate?.finishReason !== "STOP" ||
        part?.mimeType !== "audio/wav" ||
        typeof part.data !== "string" ||
        !/^[A-Za-z0-9+/]+={0,2}$/.test(part.data)
      )
        throw new SpeechFailure(502);
      const audio = Buffer.from(part.data, "base64");
      validateWav(audio);
      return { audio: audio.toString("base64"), mimeType: "audio/wav" };
    } catch (error) {
      if (error instanceof SpeechFailure) throw error;
      throw new SpeechFailure(signal.aborted ? 504 : 502);
    }
  }
}
export class FixtureSpeechProvider implements SpeechProvider {
  readonly cacheKey = "fixture-speech-v1";
  async generate(): Promise<SpeechAudio> {
    const audio = Buffer.alloc(1644);
    audio.write("RIFF", 0);
    audio.writeUInt32LE(1636, 4);
    audio.write("WAVEfmt ", 8);
    audio.writeUInt32LE(16, 16);
    audio.writeUInt16LE(1, 20);
    audio.writeUInt16LE(1, 22);
    audio.writeUInt32LE(8000, 24);
    audio.writeUInt32LE(16000, 28);
    audio.writeUInt16LE(2, 32);
    audio.writeUInt16LE(16, 34);
    audio.write("data", 36);
    audio.writeUInt32LE(1600, 40);
    return { audio: audio.toString("base64"), mimeType: "audio/wav" };
  }
}
