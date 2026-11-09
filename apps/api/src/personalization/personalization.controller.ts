import {
  BadRequestException,
  Body,
  Controller,
  Get,
  Inject,
  Post,
  Put,
  Req,
} from "@nestjs/common";
import { goalSettingsSchema } from "@contextspeak/contracts";
import type { AuthenticatedRequest } from "../auth/jwt.guard";
import { PersonalizationService } from "./personalization.service";
@Controller("v1/personalization")
export class PersonalizationController {
  constructor(
    @Inject(PersonalizationService) private service: PersonalizationService,
  ) {}
  @Get() get(@Req() req: AuthenticatedRequest) {
    return this.service.get(req.identity);
  }
  @Put("goals") goals(@Req() req: AuthenticatedRequest, @Body() body: unknown) {
    const parsed = goalSettingsSchema.safeParse(body);
    if (!parsed.success)
      throw new BadRequestException("Choose up to three supported goals.");
    return this.service.goals(req.identity, parsed.data);
  }
  @Post("suggestions") suggest(@Req() req: AuthenticatedRequest) {
    return this.service.suggest(req.identity);
  }
}
