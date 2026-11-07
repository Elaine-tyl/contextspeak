import type { CollectionSettings } from "@contextspeak/contracts";
export interface SessionSnapshot {
  token: string;
  generation: number;
}
export interface CollectionActions {
  session(): SessionSnapshot;
  get(token: string): Promise<CollectionSettings>;
  put(token: string, value: CollectionSettings): Promise<CollectionSettings>;
  apply(value: CollectionSettings): void;
}
export async function toggleCollection(actions: CollectionActions) {
  const start = actions.session();
  const current = () => {
    const next = actions.session();
    return start.token === next.token && start.generation === next.generation;
  };
  const settings = await actions.get(start.token);
  if (!current()) return false;
  const result = await actions.put(start.token, {
    ...settings,
    enabled: !settings.enabled,
  });
  if (!current()) return false;
  actions.apply(result);
  return true;
}
