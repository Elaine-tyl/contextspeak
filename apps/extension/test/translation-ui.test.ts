import { expect, it } from "vitest";
import {
  EXTENSION_PROTOCOL,
  localLanguages,
  readTranslationOptions,
} from "../src/translation-ui";
const reply = {
  ok: true,
  protocolVersion: EXTENSION_PROTOCOL,
  provider: "libretranslate-local",
  languages: localLanguages,
  preferences: {
    sourceLanguage: "auto",
    targetLanguage: "zh-Hans",
    autoTranslate: false,
  },
};
it("rejects_an_old_worker_with_actionable_reload_guidance", () => {
  expect(() =>
    readTranslationOptions({ ok: false, error: "Unknown request" }),
  ).toThrow(/reload.*extension/i);
});
it("rejects_empty_language_lists_instead_of_rendering_blank_controls", () => {
  expect(() => readTranslationOptions({ ...reply, languages: [] })).toThrow(
    /languages could not be loaded/i,
  );
});
it("normalizes_old_saved_language_codes_to_existing_options", () => {
  expect(
    readTranslationOptions({
      ...reply,
      preferences: {
        sourceLanguage: "fr",
        targetLanguage: "zh",
        autoTranslate: true,
      },
    }).preferences,
  ).toEqual({
    sourceLanguage: "auto",
    targetLanguage: "zh-Hans",
    autoTranslate: true,
  });
});

it("rejects_the_previous_plain_translation_worker_protocol", () => {
  expect(() =>
    readTranslationOptions({ ...reply, protocolVersion: 2 }),
  ).toThrow(/reload.*extension/i);
});
