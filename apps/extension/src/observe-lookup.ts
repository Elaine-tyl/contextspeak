import type { LookupReply } from "./lookup-flow";
interface Observer {
  render(reply: LookupReply): void;
  active(): boolean;
  read(eventId: string): Promise<LookupReply>;
  wait(): Promise<void>;
  limit?: number;
  enrich?(reply: LookupReply): Promise<LookupReply>;
}
export async function observeLookup(initial: LookupReply, observer: Observer) {
  let result = initial;
  if (!observer.active()) return;
  observer.render(result);
  for (let poll = 0; poll < (observer.limit ?? 30); poll++) {
    if (!result.ok || result.status === "succeeded" || !result.eventId) break;
    await observer.wait();
    if (!observer.active()) return;
    result = await observer.read(result.eventId);
    if (!observer.active()) return;
    observer.render(result);
  }
  if (
    result.ok &&
    result.status === "succeeded" &&
    observer.enrich &&
    observer.active()
  ) {
    try {
      const enriched = await observer.enrich(result);
      if (
        enriched !== result &&
        observer.active() &&
        ((enriched?.ok &&
          enriched.status === "succeeded" &&
          enriched.meaning === result.meaning) ||
          (!enriched?.ok && enriched?.status === "cancelled"))
      )
        observer.render(enriched);
    } catch {
      /* Optional expansion never replaces a successful translation with an error. */
    }
  }
}
