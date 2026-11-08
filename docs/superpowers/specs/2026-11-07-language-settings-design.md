# Extension language settings redesign

## Intent

Use the supplied language-settings screenshots as a visual reference for ContextSpeak's extension popup. Make language choices easier to find without changing the working translation-to-library flow.

## Proposed design

- Dark charcoal surfaces, white headings, muted supporting text, rounded language controls, and teal focus and toggle states.
- Separate searchable source and target language pickers. Source includes Detect automatically; both list only installed translator languages (currently English and Chinese).
- Show the selected language and a checkmark in the expanded list. Support keyboard navigation, Escape, visible focus, and clear accessible labels.
- Style automatic selection translation and collection as distinct toggle rows. Preserve explicit Save actions, per-account preferences, pairing, collection exclusions, and synchronization.
- Restyle the surrounding popup consistently; keep the webpage translation panel and learning website outside this change.

## Boundaries

Do not add regional variants, multiple primary languages, or an Add language button until the translator supports those capabilities. Do not copy branding or assets from the reference. Do not change translation, authentication, collection consent, or storage behavior.

## Approach

Recommended: an accessible React language picker styled to match the reference, keeping the current provider catalog and preference APIs. Native selects with rounded styling would be smaller but cannot reproduce a searchable expanded menu consistently across platforms. A multi-language manager would expand product scope without current model support.

## Verification

Check popup layout at its actual Chrome size, keyboard and search behavior, selected and disabled states, preference persistence, and the existing capture-to-library regressions. Ensure the tested extension build has an identifiable version so the updated popup and worker can be verified together.

## Approval

Approved by the user on 2026-11-07; implementing the popup scope above.
