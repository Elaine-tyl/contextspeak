// Maintenance requests only. Retry transient failures; never retry validation errors.
export async function downloadSource(url: string, fetcher = fetch) {
  for (let attempt = 0; attempt < 3; attempt++) {
    let response: Response;
    try {
      response = await fetcher(url, {
        redirect: "error",
        signal: AbortSignal.timeout(30000),
      });
    } catch (error) {
      if (attempt < 2) continue;
      throw new Error(`Source connection failed: ${url}`, { cause: error });
    }
    if ((response.status === 429 || response.status >= 500) && attempt < 2) {
      await response.body?.cancel();
      continue;
    }
    if (!response.ok)
      throw new Error(`Source unavailable: ${url} (${response.status})`);
    const body = await response.text();
    if (body.length > 2_000_000) throw new Error(`Oversized source: ${url}`);
    return body;
  }
  throw new Error(`Source unavailable: ${url}`);
}
