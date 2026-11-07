# Free local translation

ContextSpeak uses **LibreTranslate on your own computer**. No translation API key, subscription, Google Cloud billing, or scraped translation endpoint is involved. Google sign-in is a separate feature.

## Run

Requirements: Node.js 24, pnpm 11.25.0 and an existing `python3` for bootstrap. The automated local setup is tested on Apple Silicon macOS; its paths also support Linux. Windows bootstrap is not covered yet.

```bash
pnpm install
pnpm db:generate
pnpm build:extension
pnpm dev
```

`pnpm dev` starts the database, local translator, API and website. On first use it creates an isolated Python environment, downloads the pinned Python/model dependencies, and verifies model checksums. Subsequent starts reuse them. Allow approximately 1 GB of local storage and an internet connection for initial downloads. Translation runs on local CPU after setup.

For a fully reproducible demo without configuring Google credentials:

```bash
pnpm dev:local
```

This uses a clearly marked **simulated sign-in provider** with test learners, but **real free translation models**. Normal `pnpm dev` continues to use the configured Google OAuth client.

Optional commands:

```bash
pnpm translator:setup  # download/verify the isolated runtime and models only
pnpm translator:start  # run just the local translator on 127.0.0.1:5001
```

Only loopback ports are opened. Stop the application with Ctrl+C. A separately started translator remains running until its own terminal is stopped.

## Use in Chrome

1. Reload ContextSpeak in `chrome://extensions`, then refresh the webpage. Existing pairing credentials and local queued lookups are preserved.
2. In the extension popup, choose **Source language** and **Target language**, then **Save language settings**. Defaults are **Detect automatically → Chinese**. Initially English and Chinese are installed; the dropdowns come from the running service's actual language catalog.
3. Optionally enable **Translate automatically after selecting text**. Otherwise select text and click **Translate selection**. Both modes work with words and sentences up to 2,000 Unicode characters. The selection panel also allows a one-off language change.
4. Enable collection only if you want queries saved. Collection-off translations are transient. Collected queries retain the original languages, surrounding excerpt and a single shared translation result for both the selection panel and library.

Try: **Could you tell me where the nearest train station is?** → Chinese, or **请问最近的火车站在哪里？** → English. Short-word language detection can be ambiguous; choose the source language explicitly when needed.

Chrome restricts extension access on internal pages and the Chrome Web Store. Form fields, editable areas and hidden text remain excluded. Refresh pages opened before extension installation/reload. No full-page translation or background browsing-history collection is performed.

## Reproducibility and code boundaries

- `services/translator/pyproject.toml` + `uv.lock`: pinned isolated service dependencies, including compatible HTTP-client patches.
- `services/translator/models.json`: fixed translation and sentence-boundary model URLs with SHA-256 checksums. `install_models.py` verifies downloads before installation.
- `scripts/translator.ts`: bootstrap/start the loopback service; Google application credentials are stripped from the child environment.
- `apps/api/src/library/libretranslate.provider.ts`: small typed HTTP adapter, bounded request time, response validation and safe error codes. Only selected text is sent to the translator; page URLs and surrounding context stay in ContextSpeak.
- `ConnectionManager`: serializes language/collection/account changes. Each queued lookup keeps its original source/target pair.

No provider fallback or fabricated explanation is used on failures. The UI reports an offline service, unsupported language or timeout and indicates whether a query was saved. Retrying is explicit. The local model has different translation quality from commercial providers; inspect translations before using them as learning evidence.

LibreTranslate is an upstream AGPL-3.0 service installed from PyPI, not copied into this repository. Model sources and versions are recorded separately; retain upstream notices when redistributing runtime/model artifacts. See [LibreTranslate](https://github.com/LibreTranslate/LibreTranslate), [installation](https://docs.libretranslate.com/guides/installation/) and [API](https://docs.libretranslate.com/api/operations/translate/).

## Regression tests

```bash
pnpm test                   # contracts, real HTTP adapter against a test server, API and queue tests
pnpm test:e2e               # deterministic Chromium tests using fixture translations
pnpm test:translation:live  # actual local models + unpacked extension + saved language controls
```

Stop `pnpm dev` before browser suites; they reserve the same application ports and use disposable databases. The live suite checks automatic English→Chinese sentence translation, the shared library result, repeated encounters sharing one candidate, persisted settings, and Chinese→English translation. First-run model setup is automatic. No private browser profile or real Google account is used by the automated suites.


## Blank dropdown or no selection panel after an update

Chrome can load new popup files while retaining the old background worker. Close the popup, reload **ContextSpeak** using its circular-arrow button in `chrome://extensions`, and refresh the webpage. The popup now detects this mismatch and displays reload guidance rather than empty language controls. Source/target choices are validated against actual options, and delayed responses from another account are discarded.

Read-only page containers and pages that stop mouseup bubbling are covered by browser regressions. Editable areas, forms and hidden/private text remain excluded. Automatic translation runs only when its checkbox is enabled and saved; otherwise click **Translate selection** after highlighting text.
