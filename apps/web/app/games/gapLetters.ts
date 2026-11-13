/** The sentence already accompanies lesson audio; expose only blank lengths in the UI. */
export function gapWordLengths(text: string, parts: string[]): number[] {
  let cursor = parts[0]?.length ?? 0;
  return parts.slice(1).map((part) => {
    const end = part ? text.indexOf(part, cursor) : text.length;
    const length = Math.max(1, end - cursor);
    cursor = end + part.length;
    return length;
  });
}
