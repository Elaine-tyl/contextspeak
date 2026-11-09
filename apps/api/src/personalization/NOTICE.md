# Frequency data attribution

`frequency.json` is an adapted data snapshot by **Hermit Dave**, from [FrequencyWords / OpenSubtitles 2018 English](https://github.com/hermitdave/FrequencyWords/blob/525f9b560de45753a5ea01069454e72e9aa541c6/content/2018/en/en_50k.txt), pinned commit `525f9b560de45753a5ea01069454e72e9aa541c6`.

The source content and this adapted snapshot are licensed **[CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/)**. Upstream distinguishes its MIT-licensed programs from its CC BY-SA-licensed content in the [README](https://github.com/hermitdave/FrequencyWords/blob/525f9b560de45753a5ea01069454e72e9aa541c6/README.md). This notice applies to the data snapshot; it does not relicense unrelated application code. No endorsement is implied.

Changes: retained the first 5,000 ranked upstream rows, filtered non-word tokens with `^[a-z]+(?:['-][a-z]+)*$`, preserving counts and original rank; 4,962 entries remain. Added source metadata and SHA-256 of the JSON-serialized row array. Counts describe subtitle-domain occurrences and are not a universal spoken-English or learner-need ranking. Unknown words retain no frequency estimate.

Reproduce: retrieve `content/2018/en/en_50k.txt` from the exact pinned commit, then run `pnpm exec tsx scripts/frequency-data.ts /path/to/en_50k.txt`. Without a path the same command verifies the committed snapshot's integrity offline. Changes to this adapted data must retain attribution and its share-alike license.
