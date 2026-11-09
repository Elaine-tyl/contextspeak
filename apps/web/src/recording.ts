export async function encodeRecording(blob: Blob) {
  const bytes = new Uint8Array(await blob.arrayBuffer());
  // Recordings exceed function argument limits; convert in bounded chunks.
  const chunks: string[] = [];
  for (let offset = 0; offset < bytes.length; offset += 32768)
    chunks.push(String.fromCharCode(...bytes.subarray(offset, offset + 32768)));
  return btoa(chunks.join(""));
}
