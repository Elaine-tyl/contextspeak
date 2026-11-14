using System;
using System.IO;
using UnityEngine;

namespace ContextSpeak.Cafe
{
    public readonly struct WaveData
    {
        public readonly float[] Samples;
        public readonly int Rate;
        public WaveData(float[] samples, int rate)
        {
            Samples = samples;
            Rate = rate;
        }
    }
    public static class CafeWave
    {
        public static byte[] Encode(float[] samples, int rate, int channels)
        {
            if (samples == null || channels < 1 || channels > 2 || rate < 8000 || rate > 48000 || samples.Length % channels != 0)
                throw new FormatException("Use a supported microphone format.");
            var frames = samples.Length / channels;
            if (frames < rate / 10 || frames > rate * 120)
                throw new FormatException("Speak for 0.1 to 120 seconds.");
            using var stream = new MemoryStream(44 + frames * 2);
            using var writer = new BinaryWriter(stream);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + frames * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(rate);
            writer.Write(rate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(frames * 2);
            for (var frame = 0; frame < frames; frame++)
            {
                var sample = 0f;
                for (var channel = 0; channel < channels; channel++) sample += samples[frame * channels + channel] / channels;
                if (float.IsNaN(sample) || float.IsInfinity(sample)) sample = 0;
                writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1f, 1f) * 32767));
            }
            return stream.ToArray();
        }
        public static WaveData Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 46 || bytes.Length > 4000000) throw new FormatException("Invalid AI audio.");
            using var reader = new BinaryReader(new MemoryStream(bytes));
            string Four() => System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (Four() != "RIFF" || reader.ReadInt32() != bytes.Length - 8 || Four() != "WAVE" || Four() != "fmt " || reader.ReadInt32() != 16 || reader.ReadInt16() != 1 || reader.ReadInt16() != 1)
                throw new FormatException("AI audio must be mono PCM WAV.");
            var rate = reader.ReadInt32();
            if (rate < 8000 || rate > 48000 || reader.ReadInt32() != rate * 2 || reader.ReadInt16() != 2 || reader.ReadInt16() != 16 || Four() != "data")
                throw new FormatException("Unsupported AI audio.");
            var length = reader.ReadInt32();
            if (length != bytes.Length - 44 || length % 2 != 0) throw new FormatException("Incomplete AI audio.");
            var samples = new float[length / 2];
            for (var index = 0; index < samples.Length; index++) samples[index] = reader.ReadInt16() / 32768f;
            return new WaveData(samples, rate);
        }
    }
}
