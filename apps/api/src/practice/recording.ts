import { BadRequestException } from "@nestjs/common";
import { createHash } from "node:crypto";
import type { SpeakingUpload } from "@contextspeak/contracts";
export function recording(input: Pick<SpeakingUpload, "audio" | "mimeType">) {
  const bytes = Buffer.from(input.audio, "base64");
  const signatures: Record<SpeakingUpload["mimeType"], boolean> = {
    "audio/webm": bytes
      .subarray(0, 4)
      .equals(Buffer.from([0x1a, 0x45, 0xdf, 0xa3])),
    "audio/ogg": bytes.toString("ascii", 0, 4) === "OggS",
    "audio/wav":
      bytes.toString("ascii", 0, 4) === "RIFF" &&
      bytes.toString("ascii", 8, 12) === "WAVE",
    "audio/mp4": bytes.toString("ascii", 4, 8) === "ftyp",
  };
  if (
    bytes.length < 16 ||
    bytes.length > 2 * 1024 * 1024 ||
    bytes.toString("base64") !== input.audio ||
    !signatures[input.mimeType]
  )
    throw new BadRequestException(
      "Choose a supported recording smaller than 2 MB.",
    );
  return createHash("sha256").update(bytes).digest("hex");
}
