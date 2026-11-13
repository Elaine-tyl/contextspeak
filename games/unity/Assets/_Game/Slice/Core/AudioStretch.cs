using System;

namespace ContextSpeak.Slice
{
    /// Pitch-preserving time stretch (windowed overlap-add with a short similarity search, WSOLA-style).
    /// Used for the "Slower" button and for band-based speaking speed, so voices never sound chipmunk-like.
    public static class AudioStretch
    {
        /// rate &lt; 1 slows down, rate &gt; 1 speeds up. Returns a new buffer.
        public static float[] Stretch(float[] input, int sampleRate, float rate)
        {
            if (input == null || input.Length == 0) return new float[0];
            rate = Math.Max(0.5f, Math.Min(rate, 1.6f));
            if (Math.Abs(rate - 1f) < 0.01f) return (float[])input.Clone();
            var frame = Math.Max(256, sampleRate / 25);         // 40 ms
            var hop = frame / 2;                                // synthesis hop
            var seek = frame / 4;                               // similarity search range
            var window = new float[frame];
            for (var i = 0; i < frame; i++) window[i] = 0.5f - 0.5f * (float)Math.Cos(2 * Math.PI * i / (frame - 1));
            var outputLength = (int)(input.Length / rate) + frame;
            var output = new float[outputLength];
            var weight = new float[outputLength];
            var previous = 0;
            for (var outPos = 0; outPos + frame < outputLength; outPos += hop)
            {
                var nominal = (int)(outPos * rate);
                if (nominal + frame >= input.Length) break;
                // Pick the input offset that best continues the previous frame (avoids phasey artefacts).
                var best = nominal;
                if (outPos > 0)
                {
                    var bestScore = double.MinValue;
                    var reference = previous + hop;
                    for (var candidate = Math.Max(0, nominal - seek); candidate <= Math.Min(input.Length - frame - 1, nominal + seek); candidate += 4)
                    {
                        double score = 0;
                        for (var i = 0; i < hop && reference + i < input.Length && candidate + i < input.Length; i += 4) score += input[reference + i] * input[candidate + i];
                        if (score > bestScore) { bestScore = score; best = candidate; }
                    }
                }
                for (var i = 0; i < frame; i++)
                {
                    output[outPos + i] += input[best + i] * window[i];
                    weight[outPos + i] += window[i];
                }
                previous = best;
            }
            var length = 0;
            for (var i = 0; i < outputLength; i++)
            {
                if (weight[i] > 0.001f) { output[i] /= weight[i]; length = i + 1; }
            }
            Array.Resize(ref output, length);
            return output;
        }
    }
}
