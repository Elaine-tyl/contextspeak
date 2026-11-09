import { Controller, Get, Inject, Res, SetMetadata } from "@nestjs/common";
import type { Response } from "express";
import { OperationsService } from "./operations.service";

@Controller()
export class OperationsController {
  constructor(
    @Inject(OperationsService) private readonly operations: OperationsService,
  ) {}

  @Get("health/live")
  @SetMetadata("auth:public", true)
  live() {
    return { status: "alive" };
  }

  @Get("health/ready")
  @SetMetadata("auth:public", true)
  async ready(@Res({ passthrough: true }) res: Response) {
    const result = await this.operations.readiness();
    res.status(result.status === "ready" ? 200 : 503);
    return result;
  }

  // Authenticated; process-local counters reset on restart.
  @Get("v1/operations")
  metrics() {
    return this.operations.snapshot();
  }
}
