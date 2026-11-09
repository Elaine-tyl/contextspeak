# Open practice content

The shared practice corpus contains **48 reviewed English excerpts** from **30 source documents**. It adds 48 dictation tasks and 48 open speaking questions to the original 83 games: **179 tasks in total**, using the existing ten difficulty levels, AI audio cache, scoring and saved game receipts. It does not add entries to a user's personal vocabulary library.

| Collection | Imported text | Reuse basis |
| --- | --- | --- |
| Tatoeba | 24 everyday sentences | Each selected API record explicitly states **CC0 1.0**, has an owner, is English and is not flagged unapproved. |
| VOA Learning English | 12 excerpts from two introductory lessons | Original VOA educational text; credited under the publisher's public-domain content policy. No AP/Reuters/AFP material. |
| African Storybook / Global Storybooks | 12 excerpts from four stories | Each selected story is **CC BY 3.0**, according to the pinned language index, with author credit in the source file. No CC BY-NC stories. |

The first collection covers help, meeting people, time, plans and libraries. VOA covers greetings, introductions and neighbours. Story excerpts provide short actions and progressively longer connected narratives. Difficulty is an editorial product label, not a CEFR assessment. Questions are written by ContextSpeak and allow multiple appropriate spoken answers.

## Use in the app

Open `/games`, choose Listening or Speaking, a game mode, an unlocked level and theme. **Content source** filters the collection; Start level begins up to three matching questions. The completed level shows its score with Retry and Next. Original games remain available. Imported rounds display a source link, author and license link. Listening text is revealed only after checking the answer. Playback uses generated AI audio, not publisher recordings or an imitation of a publisher's narrator. Learner recordings follow the existing assessment flow.

## Snapshot and provenance

[corpus.json](../apps/api/src/content/corpus.json) stores the reviewed excerpts, SHA-256 of each text, source document, author/contributor, source and license URLs, topic, question and level. Review date: **2026-11-09**. African Storybook source files are pinned to commit `b5c3d5b2266ad936e5f20fa878e839b32b02b4ba`; the [English index at that commit](https://github.com/global-asp/asp-source/blob/b5c3d5b2266ad936e5f20fa878e839b32b02b4ba/en/README.md) supplies the license version. Author Letta Machoga is also credited as translator of *I like to read!* in its source file.

The only text processing is selecting excerpts and normalising whitespace, including joining story pages for one reading task. Words are not simplified or silently rewritten. Each exercise is an adaptation around that credited text. Text licenses apply to the imported content separately from the application code. No publisher logos, illustrations, video, recordings or third-party translations are imported. No endorsement is implied.

## Reproduce and maintain

```sh
pnpm install --frozen-lockfile
pnpm content:verify
pnpm content:verify --online
```

The default command validates committed data offline. `--online` additionally checks every allowlisted source: current Tatoeba language/license/owner/approval and text, original VOA excerpt presence, or pinned story author/license and excerpt presence. A connection failure or content change fails the command; it never edits historical tasks. It sends no learner data, credentials or API keys. Practice startup and playback do not contact the source sites.

To expand the corpus, inspect the publisher's rights terms and the individual work, then add a reviewed source and bounded excerpt with a new ID, topic, question, level and `textDigest(text)` SHA-256. Run both validation modes and regression tests. Existing task text, question, criteria and IDs are immutable: corrections need a new revision and ID. Unrestricted crawling or automatic publication of unreviewed search results is intentionally absent.

## Publisher references

- [Tatoeba corpus reuse guidance](https://en.wiki.tatoeba.org/articles/show/using-the-tatoeba-corpus), [official API documentation](https://api.tatoeba.org/openapi), [CC0 terms](https://creativecommons.org/publicdomain/zero/1.0/). Default Tatoeba text licensing and audio licensing differ; this snapshot selects explicit CC0 text only.
- [VOA reuse policy](https://learningenglish.voanews.com/p/6861.html), [copyright statement](https://learningenglish.voanews.com/p/6021.html), [Welcome!](https://learningenglish.voanews.com/a/lets-learn-english-lesson-one/3111026.html), [Hello, I'm Anna!](https://learningenglish.voanews.com/a/lets-learn-english-lesson-2-hello/3113733.html).
- [African Storybook source repository](https://github.com/global-asp/asp-source), [Global Storybooks reuse FAQ](https://globalstorybooks.net/faq/), [CC BY 3.0 terms](https://creativecommons.org/licenses/by/3.0/).
- [Project Gutenberg reuse policy](https://www.gutenberg.org/policy/license): researched but not imported in this batch because its public-domain designation is US-specific. A globally deployed collection needs market-specific review before using those books.
