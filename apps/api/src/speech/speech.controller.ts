import {
  BadRequestException,
  Body,
  Controller,
  Inject,
  Post,
  Req,
} from "@nestjs/common";
import { speechInputSchema } from "@contextspeak/contracts";
import type { AuthenticatedRequest } from "../auth/jwt.guard";
import { SpeechService } from "./speech.service";
@Controller("v1/speech")
export class SpeechController {
  constructor(@Inject(SpeechService) private speech: SpeechService) {}
  @Post() speak(@Req() req: AuthenticatedRequest, @Body() body: unknown) {
    const parsed = speechInputSchema.safeParse(body);
    if (!parsed.success)
      throw new BadRequestException(
        "Choose supported-language text of at most 600 characters.",
      );
    return this.speech.speak(req.identity, parsed.data);
  }
}
