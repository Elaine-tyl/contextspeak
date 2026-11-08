import {
  BadRequestException,
  Controller,
  Get,
  Inject,
  Param,
  Post,
  Req,
  Body,
} from "@nestjs/common";
import {
  reviewSubmissionSchema,
  reviewEnrollmentSchema,
} from "@contextspeak/contracts";
import type { AuthenticatedRequest } from "../auth/jwt.guard";
import { ReviewService } from "./review.service";
@Controller("v1/reviews")
export class ReviewController {
  constructor(@Inject(ReviewService) private reviews: ReviewService) {}
  @Get() queue(@Req() req: AuthenticatedRequest) {
    return this.reviews.queue(req.identity);
  }
  @Post(":id/enroll") enroll(
    @Req() req: AuthenticatedRequest,
    @Param("id") id: string,
    @Body() body: unknown,
  ) {
    const parsed = reviewEnrollmentSchema.safeParse(body);
    if (!parsed.success)
      throw new BadRequestException("Invalid review enrollment.");
    return this.reviews.enroll(req.identity, id, parsed.data.lookupId);
  }
  @Post(":id/pause") pause(
    @Req() req: AuthenticatedRequest,
    @Param("id") id: string,
  ) {
    return this.reviews.pause(req.identity, id);
  }
  @Post(":id/rate") rate(
    @Req() req: AuthenticatedRequest,
    @Param("id") id: string,
    @Body() body: unknown,
  ) {
    const parsed = reviewSubmissionSchema.safeParse(body);
    if (!parsed.success)
      throw new BadRequestException("Invalid review rating.");
    return this.reviews.rate(req.identity, id, parsed.data);
  }
}
