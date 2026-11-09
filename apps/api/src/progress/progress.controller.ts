import { Controller, Get, Inject, Req } from "@nestjs/common";
import type { AuthenticatedRequest } from "../auth/jwt.guard";
import { ProgressService } from "./progress.service";
@Controller("v1/progress")
export class ProgressController {
  constructor(@Inject(ProgressService) private progress: ProgressService) {}
  @Get() get(@Req() req: AuthenticatedRequest) {
    return this.progress.get(req.identity);
  }
}
