import { expect, it } from "vitest";
import { access, readFile, stat } from "node:fs/promises";
import { transcribeLocal } from "./providers";

for (const fails of [false, true]) {
  it(`removes private temporary recordings after ${fails ? "failed" : "successful"} recognition`, async () => {
    let path = "";
    const work = transcribeLocal(
      Buffer.from("private test audio"),
      "/not-a-real-model",
      async (_binary, args) => {
        path = args[args.indexOf("-f") + 1];
        expect((await stat(path)).mode & 0o777).toBe(0o600);
        expect(await readFile(path, "utf8")).toBe("private test audio");
        if (fails) throw new Error("recognizer unavailable");
        return { stdout: "Two coffees, please." };
      },
    );
    if (fails) await expect(work).rejects.toThrow("recognizer unavailable");
    else expect(await work).toBe("Two coffees, please.");
    await expect(access(path)).rejects.toThrow();
  });
}
