import {
  BadRequestException,
  Body,
  Controller,
  Get,
  Inject,
  Post,
  Req,
} from "@nestjs/common";
import { gameSubmissionSchema } from "@contextspeak/contracts";
import type { AuthenticatedRequest } from "../auth/jwt.guard";
import { GamesService } from "./games.service";
@Controller("v1/games")
export class GamesController {
  constructor(@Inject(GamesService) private games: GamesService) {}
  @Get() catalog(@Req() req: AuthenticatedRequest) {
    return this.games.catalog(req.identity);
  }
  @Post("attempts") submit(
    @Req() req: AuthenticatedRequest,
    @Body() body: unknown,
  ) {
    const parsed = gameSubmissionSchema.safeParse(body);
    if (!parsed.success)
      throw new BadRequestException(
        "Invalid game answer. Maximum recording: 60 seconds and 2 MB.",
      );
    return this.games.submit(req.identity, parsed.data);
  }
}
