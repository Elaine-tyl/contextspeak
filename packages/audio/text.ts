/** Keep a continuous passage within the existing 600-codepoint voice limit. */
export function splitSpeechText(text: string): string[] {
  const value = text.trim();
  if (!value || [...value].length > 2000) return [];
  if ([...value].length <= 600) return [value];
  const chunks: string[] = [];
  let current = "";
  function append(part: string) {
    const combined = current ? `${current} ${part}` : part;
    if ([...combined].length <= 600) current = combined;
    else {
      if (current) chunks.push(current);
      current = part;
    }
  }
  for (const { segment } of new Intl.Segmenter("en", {
    granularity: "sentence",
  }).segment(value)) {
    let characters = [...segment.trim()];
    while (characters.length > 600) {
      let end = 600;
      while (end > 0 && !/\s/u.test(characters[end])) end--;
      if (!end) end = 600;
      append(characters.slice(0, end).join("").trim());
      characters = [...characters.slice(end).join("").trim()];
    }
    if (characters.length) append(characters.join(""));
  }
  if (current) chunks.push(current);
  return chunks;
}
