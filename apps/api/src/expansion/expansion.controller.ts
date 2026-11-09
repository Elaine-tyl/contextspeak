import {
  BadRequestException,
  Body,
  Controller,
  Inject,
  Post,
  Req,
} from "@nestjs/common";
import { z } from "zod";
import { aiLanguageCodes, translationSchema } from "@contextspeak/contracts";
import type { AuthenticatedRequest } from "../auth/jwt.guard";
import { ExpansionService } from "./expansion.service";
const schema = z
  .object({
    input: translationSchema.extend({
      sourceLanguage: z.literal("en"),
      targetLanguage: z.enum(aiLanguageCodes),
    }),
    meaning: z.string().min(1).max(4000),
    lookupId: z.uuid().optional(),
  })
  .strict();
@Controller("v1/expansions")
export class ExpansionController {
  constructor(@Inject(ExpansionService) private expansion: ExpansionService) {}
  @Post() expand(@Req() req: AuthenticatedRequest, @Body() body: unknown) {
    const value = schema.safeParse(body);
    if (!value.success)
      throw new BadRequestException("Invalid word expansion request");
    return this.expansion.get(req.identity, value.data);
  }
}
