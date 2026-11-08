import {
  BadRequestException,
  Body,
  Controller,
  Get,
  Inject,
  Param,
  Post,
  Query,
  Req,
} from "@nestjs/common";
import {
  practiceSubmissionSchema,
  practiceQuerySchema,
} from "@contextspeak/contracts";
import type { AuthenticatedRequest } from "../auth/jwt.guard";
import { libraryFiltersSchema } from "../library/library-filters";
import { PracticeService } from "./practice.service";
@Controller("v1/practice")
export class PracticeController {
  constructor(@Inject(PracticeService) private practice: PracticeService) {}
  @Get() list(@Req() req: AuthenticatedRequest, @Query() query: unknown) {
    const parsed = libraryFiltersSchema.safeParse(query);
    if (!parsed.success)
      throw new BadRequestException("Invalid practice filters.");
    return this.practice.list(req.identity, parsed.data);
  }
  @Get(":id") task(
    @Req() req: AuthenticatedRequest,
    @Param("id") id: string,
    @Query() query: unknown,
  ) {
    const parsed = practiceQuerySchema.safeParse(query);
    if (!parsed.success)
      throw new BadRequestException("Invalid practice scenario or encounter.");
    return this.practice.task(
      req.identity,
      id,
      parsed.data.lookup,
      parsed.data.scenario,
    );
  }
  @Post(":id/attempts") save(
    @Req() req: AuthenticatedRequest,
    @Param("id") id: string,
    @Body() body: unknown,
  ) {
    const parsed = practiceSubmissionSchema.safeParse(body);
    if (!parsed.success)
      throw new BadRequestException(
        "Invalid practice result. Complete playback or recording first.",
      );
    return this.practice.save(req.identity, id, parsed.data);
  }
}
