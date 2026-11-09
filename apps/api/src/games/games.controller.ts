import {
  BadRequestException,
  Body,
  Controller,
  Get,
  Inject,
  Post,
  Param,
  Req,
} from "@nestjs/common";
import {
  gameSubmissionSchema,
  gameLevelStartSchema,
} from "@contextspeak/contracts";
import type { AuthenticatedRequest } from "../auth/jwt.guard";
import { GamesService } from "./games.service";
@Controller("v1/games")
export class GamesController {
  constructor(@Inject(GamesService) private games: GamesService) {}
  @Get() catalog(@Req() req: AuthenticatedRequest) {
    return this.games.catalog(req.identity);
  }
  @Post("levels") startLevel(
    @Req() req: AuthenticatedRequest,
    @Body() body: unknown,
  ) {
    const parsed = gameLevelStartSchema.safeParse(body);
    if (!parsed.success)
      throw new BadRequestException(
        "Choose a game mode and a level from 1 to 10.",
      );
    return this.games.levels.start(req.identity, parsed.data);
  }
  @Get("levels/:id") level(
    @Req() req: AuthenticatedRequest,
    @Param("id") id: string,
  ) {
    return this.games.levels.get(req.identity, id);
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
