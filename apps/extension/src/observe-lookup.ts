import type { LookupReply } from "./lookup-flow";
interface Observer {
  render(reply: LookupReply): void;
  active(): boolean;
  read(eventId: string): Promise<LookupReply>;
  wait(): Promise<void>;
  limit?: number;
}
export async function observeLookup(initial: LookupReply, observer: Observer) {
  let result = initial;
  if (!observer.active()) return;
  observer.render(result);
  for (let poll = 0; poll < (observer.limit ?? 30); poll++) {
    if (!result.ok || result.status === "succeeded" || !result.eventId) return;
    await observer.wait();
    if (!observer.active()) return;
    result = await observer.read(result.eventId);
    if (!observer.active()) return;
    observer.render(result);
  }
}
