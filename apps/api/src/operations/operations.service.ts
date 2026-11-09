export class OperationsService {
  private readonly startedAt = Date.now();
  private requests = 0;
  private serverErrors = 0;
  private inFlight?: Promise<unknown>;
  constructor(
    private probe: () => Promise<unknown>,
    private timeoutMs = 1000,
  ) {}
  async readiness(): Promise<{ status: "ready" | "unavailable" }> {
    let timer: ReturnType<typeof setTimeout> | undefined;
    try {
      if (!this.inFlight) {
        this.inFlight = Promise.resolve().then(this.probe);
        // Retain a hung probe after the response timeout instead of exhausting the pool.
        void this.inFlight.then(
          () => {
            this.inFlight = undefined;
          },
          () => {
            this.inFlight = undefined;
          },
        );
      }
      await Promise.race([
        this.inFlight,
        new Promise((_, reject) => {
          timer = setTimeout(
            () => reject(new Error("Readiness timeout")),
            this.timeoutMs,
          );
        }),
      ]);
      return { status: "ready" };
    } catch {
      return { status: "unavailable" };
    } finally {
      clearTimeout(timer);
    }
  }
  recordRequest(statusCode: number) {
    this.requests++;
    if (statusCode >= 500) this.serverErrors++;
  }
  snapshot() {
    return {
      requests: this.requests,
      serverErrors: this.serverErrors,
      uptimeSeconds: Math.floor((Date.now() - this.startedAt) / 1000),
    };
  }
}
