# Open practice content

The shared practice corpus contains **66 reviewed English excerpts** from **34 source documents**. Current Games uses the 18 reviewed news excerpts for **54 active source tasks**: 18 dictations, 18 speaking discussions (v2) and 18 missing-word exercises. Together with 130 current originals, there are **184 active tasks**. Foundational imports and earlier speaking editions remain archived for saved runs and scores. The full source catalog retains 168 versioned tasks; startup still validates every retained excerpt. It does not add entries to a user's personal vocabulary library.

| Collection | Imported text | Reuse basis |
| --- | --- | --- |
| Tatoeba | 24 everyday sentences | Each selected API record explicitly states **CC0 1.0**, has an owner, is English and is not flagged unapproved. |
| VOA Learning English | 12 excerpts from two introductory lessons and 18 from four staff-authored reports | Original VOA educational text; credited under the publisher's public-domain content policy. No AP/Reuters/AFP material. |
| African Storybook / Global Storybooks | 12 excerpts from four stories | Each selected story is **CC BY 3.0**, according to the pinned language index, with author credit in the source file. No CC BY-NC stories. |

The first collection covers help, meeting people, time, plans and libraries. VOA covers greetings, introductions, neighbours, academic writing/research, AI/history, workplace performance and carbon-reduction research. The education report was published on 2024-02-24 and written by Dan Friedell; selected staff narration excludes interview quotations and syndicated material. It is archival reading, not a current-news feed. Story excerpts provide short actions and progressively longer connected narratives. Difficulty is an editorial product label, not a CEFR assessment. Questions are written by ContextSpeak and allow multiple appropriate spoken answers.

## Use in the app

Open `/games`, choose Listening or Speaking, a game mode, an unlocked level and theme. Current source filtering offers the reviewed VOA news reports; Tatoeba, introductory VOA lessons and story excerpts are retained in historical results. **Content source** filters the collection; Start level begins up to three matching questions. The completed level shows its score with Retry and Next. Original games remain available. Imported rounds display a source link, author and license link. Listening text is revealed only after checking the answer. Playback uses generated AI audio, not publisher recordings or an imitation of a publisher's narrator. Learner recordings follow the existing assessment flow.

## Snapshot and provenance

[corpus.json](../apps/api/src/content/corpus.json) stores the reviewed excerpts, SHA-256 of each text, source document, author/contributor, source and license URLs, topic, question and level. Review date: **2026-11-11**. African Storybook source files are pinned to commit `b5c3d5b2266ad936e5f20fa878e839b32b02b4ba`; the [English index at that commit](https://github.com/global-asp/asp-source/blob/b5c3d5b2266ad936e5f20fa878e839b32b02b4ba/en/README.md) supplies the license version. Author Letta Machoga is also credited as translator of *I like to read!* in its source file.

The only text processing is selecting excerpts and normalising whitespace, including joining story pages for one reading task. Words are not simplified or silently rewritten. Each exercise is an adaptation around that credited text. Text licenses apply to the imported content separately from the application code. No publisher logos, illustrations, video, recordings or third-party translations are imported. No endorsement is implied.

## Reproduce and maintain

```sh
pnpm install --frozen-lockfile
pnpm content:verify
pnpm content:verify --online
```

The default command validates committed data offline. `--online` additionally checks every allowlisted source: current Tatoeba language/license/owner/approval and text, original VOA excerpt presence and the reviewed news report’s staff authorship line, or pinned story author/license and excerpt presence. A connection failure or content change fails the command; it never edits historical tasks. It sends no learner data, credentials or API keys. Practice startup and playback do not contact the source sites.

To expand the corpus, inspect the publisher's rights terms and the individual work, then add a reviewed source and bounded excerpt with a new ID, topic, question, level and `textDigest(text)` SHA-256. Run both validation modes and regression tests. Existing task text, question, criteria and IDs are immutable: corrections need a new revision and ID. Unrestricted crawling or automatic publication of unreviewed search results is intentionally absent.

## Publisher references

- [Tatoeba corpus reuse guidance](https://en.wiki.tatoeba.org/articles/show/using-the-tatoeba-corpus), [official API documentation](https://api.tatoeba.org/openapi), [CC0 terms](https://creativecommons.org/publicdomain/zero/1.0/). Default Tatoeba text licensing and audio licensing differ; this snapshot selects explicit CC0 text only.
- [VOA reuse policy](https://learningenglish.voanews.com/p/6861.html), [copyright statement](https://learningenglish.voanews.com/p/6021.html), [Welcome!](https://learningenglish.voanews.com/a/lets-learn-english-lesson-one/3111026.html), [Hello, I'm Anna!](https://learningenglish.voanews.com/a/lets-learn-english-lesson-2-hello/3113733.html).
- [African Storybook source repository](https://github.com/global-asp/asp-source), [Global Storybooks reuse FAQ](https://globalstorybooks.net/faq/), [CC BY 3.0 terms](https://creativecommons.org/licenses/by/3.0/).
- [Project Gutenberg reuse policy](https://www.gutenberg.org/policy/license): researched but not imported in this batch because its public-domain designation is US-specific. A globally deployed collection needs market-specific review before using those books.

## News and IELTS-style listening

The 18 news gap templates reconstruct the hashed original text exactly. They
use 2–6 single-word blanks and display the article publication date. The four exact
reviewed reports are allowlisted in `content/reviewed-news.ts`; arbitrary VOA, AP, Reuters or AFP stories are not
accepted. [Read the source report](https://learningenglish.voanews.com/a/how-international-students-should-think-about-writing-ai/7498850.html).

**Academic study** provides ten independently written gap exercises. These are
IELTS-style academic listening practice, not official test questions, validated
IELTS band estimates or an exam simulation. Repeated playback remains available.
For official free material, open [IELTS sample questions](https://ielts.org/take-a-test/preparation-resources/sample-test-questions)
or [British Council listening practice](https://takeielts.britishcouncil.org/prepare/ielts-free-practice-mock-tests/academic/listening).
Free study access is distinct from permission to republish or adapt: [IELTS’s
copyright statement](https://ielts.org/legal/ielts-copyright-and-trade-mark-statement)
restricts reuse, so their questions and recordings are linked, not imported.


## Intermediate–advanced news vocabulary

Twelve additional staff-narration excerpts were verified from these archive
reports (publisher images/audio and interview quotations are excluded):

| Topic | Report | Author | Published |
| --- | --- | --- | --- |
| Technology/history | [AI and ancient inscriptions](https://learningenglish.voanews.com/a/ai-used-to-fill-in-missing-words-in-ancient-writings/6488034.html) | Bryan Lynn | 2022-03-16 |
| Work | [AI and worker performance](https://learningenglish.voanews.com/a/study-finds-ai-tools-help-but-also-hurt-worker-performance/7296498.html/) | Bryan Lynn | 2023-10-04 |
| Environment | [Carbon dioxide and fuel](https://learningenglish.voanews.com/a/researchers-find-way-to-turn-carbon-dioxide-into-valuable-fuel/5090884.html) | Bryan Lynn | 2019-09-22 |

The manifest pins each report's path, author, publication date and exact staff
attribution sentence. Validation rejects unreviewed paths/query strings, changed
metadata/bylines and gap templates that fail to reconstruct the hashed excerpt.
A Reuters/AP photo credit in a page is not treated as a license to import that
photograph; only the inspected, independently credited staff narration is used.

BBC offers free study resources such as this [news-vocabulary worksheet](https://downloads.bbc.co.uk/learningenglish/features/LEFTN/250827_LEFTN_heatwaves_speed_up_ageing_process_study_says_worksheet.pdf)
and [Learning English programmes](https://www.bbc.co.uk/learningenglish).
Their material opens externally. [BBC's copyright notice](https://www.bbcworldnews.com/pages/Copyright.aspx)
requires permission for uses beyond its stated personal use; free access is not
assumed to permit storing/adapting BBC articles in this product.


## Difficulty alignment and history

News dictations retain their original text and v1 IDs. News speaking v2 adds
explicit reasoning criteria matched to the shared difficulty band; its audio
includes the question and requested reasoning. Earlier speaking v1 instructions,
answers and criteria stay unchanged in archived runs. The underlying corpus and
licenses are unchanged. New selection omits the shorter foundational tasks rather
than relabelling a one-word exercise as intermediate or advanced. Retry from an
archived-only source uses current content, with a visible earlier-edition notice.

### Reading game packs (12 November 2026)

The reading game now includes 15 original comprehension questions built around
five credited texts: the complete *A very tall man* story (Cornelius Gulere,
African Storybook, CC BY 3.0), and selected excerpts from the four already-reviewed
VOA science/education reports. The news packs are joined excerpts, not complete
articles or current-news updates. Original publication dates remain visible.

The complete story was downloaded from the pinned Global Storybooks source above;
its author and explicit CC BY 3.0 declaration were checked against the collection
license index. Its text and SHA-256 are committed in `corpus.json`, so the existing
`pnpm content:verify --online` command also verifies it. No illustrations or
third-party news-agency media were copied. Questions and distractors are written
by ContextSpeak; these are practice levels, not official IELTS examinations.

Reading packs appear at levels 1, 3, 5, 7 and 9. Every mode still has original
questions at all ten levels; choose All sources to include them. Text games need
neither speech-generation quota nor a live content-provider connection.
