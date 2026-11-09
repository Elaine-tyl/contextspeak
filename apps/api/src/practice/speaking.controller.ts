import {
  BadRequestException,
  Body,
  Controller,
  Inject,
  Param,
  Post,
  Req,
} from "@nestjs/common";
import { speakingUploadSchema } from "@contextspeak/contracts";
import type { AuthenticatedRequest } from "../auth/jwt.guard";
import { SpeakingService } from "./speaking.service";
@Controller("v1/practice")
export class SpeakingController {
  constructor(@Inject(SpeakingService) private speaking: SpeakingService) {}
  @Post(":id/speaking-assessment") submit(
    @Req() req: AuthenticatedRequest,
    @Param("id") id: string,
    @Body() body: unknown,
  ) {
    const parsed = speakingUploadSchema.safeParse(body);
    if (!parsed.success)
      throw new BadRequestException(
        "Replay a recording of up to 60 seconds and consent to AI assessment. Maximum upload: 2 MB.",
      );
    return this.speaking.submit(req.identity, id, parsed.data);
  }
}
