import type {
  CollectionSettings,
  TranslationPreferences,
} from "@contextspeak/contracts";
export interface LocalConnection extends CollectionSettings {
  translationPreferences?: TranslationPreferences;
  token: string;
  identity: string;
}
export interface ConnectionDependencies {
  read(): Promise<LocalConnection>;
  write(value: LocalConnection): Promise<void>;
  authenticate(token: string): Promise<LocalConnection>;
  putTranslationPreferences?(
    value: TranslationPreferences,
    token: string,
  ): Promise<TranslationPreferences>;
  putSettings(value: CollectionSettings, token?: string): Promise<unknown>;
}
// All account/settings transitions share one queue; a new connect invalidates stale work immediately.
export class ConnectionManager {
  private revision = 0;
  private pending: Promise<unknown> = Promise.resolve();
  constructor(private deps: ConnectionDependencies) {}
  private serialize<T>(fn: () => Promise<T>) {
    const result = this.pending.then(fn);
    this.pending = result.catch(() => {});
    return result;
  }
  connect(token: string) {
    const revision = ++this.revision;
    return this.serialize(async () => {
      const value = await this.deps.authenticate(token);
      if (revision !== this.revision) throw new Error("Connection changed");
      await this.deps.write(value);
    });
  }
  disconnect() {
    ++this.revision;
    return this.serialize(() =>
      this.deps.write({
        token: "",
        identity: "",
        enabled: false,
        excludedOrigins: [],
      }),
    );
  }
  saveTranslationPreferences(value: TranslationPreferences) {
    const revision = this.revision;
    return this.serialize(async () => {
      const current = await this.deps.read();
      if (revision !== this.revision || !this.deps.putTranslationPreferences)
        throw new Error("Connection changed");
      const saved = await this.deps.putTranslationPreferences(
        value,
        current.token,
      );
      if (revision !== this.revision) throw new Error("Connection changed");
      await this.deps.write({ ...current, translationPreferences: saved });
    });
  }
  saveSettings(value: CollectionSettings) {
    const revision = this.revision;
    return this.serialize(async () => {
      if (revision !== this.revision) throw new Error("Connection changed");
      const current = await this.deps.read();
      if (revision !== this.revision) throw new Error("Connection changed");
      await this.deps.write({ ...current, ...value, enabled: false });
      if (revision !== this.revision) throw new Error("Connection changed");
      await this.deps.putSettings(value, current.token);
      if (revision !== this.revision) throw new Error("Connection changed");
      await this.deps.write({ ...current, ...value });
    });
  }
}
