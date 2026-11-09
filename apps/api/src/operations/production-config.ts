import { isIP } from "node:net";

export interface ProductionConfig {
  nodeEnv?: string;
  testAuth?: boolean;
  oidc?: {
    issuer: string;
    apiOrigin: string;
    webOrigin: string;
    allowLoopbackHttp?: boolean;
  };
  origins?: string[];
  providers?: {
    translation?: object;
    speech?: object;
    speaking?: object;
    expansion?: object;
    scenario?: object;
    speechFallback?: object;
  };
}
function publicHttps(value: string, originOnly: boolean) {
  try {
    const url = new URL(value);
    const host = url.hostname
      .replace(/^\[|\]$/g, "")
      .replace(/\.$/, "")
      .toLowerCase();
    return (
      url.protocol === "https:" &&
      !url.username &&
      !url.password &&
      !url.search &&
      !url.hash &&
      (!originOnly || url.origin === value) &&
      !isIP(host) &&
      /^(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z](?:[a-z0-9-]{0,61}[a-z0-9])?$/.test(
        host,
      ) &&
      !/(^|\.)(localhost|local|internal)$/.test(host)
    );
  } catch {
    return false;
  }
}

/** Startup guard only: does not verify credentials, DNS, quotas or provider quality. */
export function assertProductionConfig(config: ProductionConfig) {
  if ((config.nodeEnv ?? process.env.NODE_ENV) !== "production") return;
  if (config.testAuth || !config.oidc || config.oidc.allowLoopbackHttp)
    throw new Error(
      "Production requires real OIDC without development overrides",
    );
  const { issuer, apiOrigin, webOrigin } = config.oidc;
  if (
    !publicHttps(issuer, false) ||
    !publicHttps(apiOrigin, true) ||
    !publicHttps(webOrigin, true)
  )
    throw new Error("Production authentication requires public HTTPS URLs");
  for (const origin of config.origins ?? [webOrigin]) {
    if (
      !publicHttps(origin, true) &&
      !/^chrome-extension:\/\/[a-p]{32}$/.test(origin)
    )
      throw new Error(
        "Production CORS origins must be explicit HTTPS or extension origins",
      );
  }
  if (
    !config.providers?.translation ||
    !config.providers.speech ||
    !config.providers.speaking
  )
    throw new Error(
      "Production requires translation, speech and speaking providers",
    );
  for (const provider of Object.values(config.providers)) {
    if (!provider) continue;
    const descriptor = provider as { name?: string; cacheKey?: string };
    if (
      /fixture|simulat|mock|fake/i.test(
        [descriptor.name, descriptor.cacheKey, provider.constructor?.name].join(
          " ",
        ),
      )
    )
      throw new Error("Simulated providers are forbidden in production");
  }
}
