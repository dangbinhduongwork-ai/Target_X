using System;
using System.IO;
using System.Media;
using System.Text;
using System.Threading.Tasks;

namespace GoalOverlay.Services;

/// <summary>
/// Trình phát âm thanh thông báo độc lập, tự tạo sóng âm thanh PCM WAV trực tiếp trong bộ nhớ.
/// Đảm bảo phát ra tiếng chuông trên mọi máy tính Windows mà không phụ thuộc vào thiết lập "Sound Scheme" của hệ điều hành.
/// </summary>
public static class SoundHelper
{
    private static byte[]? _cachedChimeWav;
    private static SoundPlayer? _player;
    private static readonly object _lock = new();

    /// <summary>
    /// Phát tiếng chuông thông báo trong trẻo (hai âm C5 -> G5)
    /// </summary>
    public static void PlayNotificationChime()
    {
        Task.Run(() =>
        {
            try
            {
                lock (_lock)
                {
                    if (_player == null)
                    {
                        _cachedChimeWav ??= GeneratePleasantChimeWav();
                        var ms = new MemoryStream(_cachedChimeWav);
                        _player = new SoundPlayer(ms);
                        _player.Load();
                    }

                    _player.PlaySync();
                }
                return;
            }
            catch { }

            // Fallback 1: Chuông hệ thống mặc định
            try
            {
                SystemSounds.Exclamation.Play();
                return;
            }
            catch { }

            // Fallback 2: Beep hệ thống
            try
            {
                Console.Beep(880, 250);
            }
            catch { }
        });
    }

    /// <summary>
    /// Tạo file WAV chuẩn PCM (44.1kHz, 16-bit Mono) với âm sắc chuông hai nốt êm tai
    /// </summary>
    private static byte[] GeneratePleasantChimeWav()
    {
        int sampleRate = 44100;
        double durationSeconds = 0.55;
        int numSamples = (int)(sampleRate * durationSeconds);
        short[] samples = new short[numSamples];

        // Hai nốt nhạc: Nốt 1 C5 (523.25 Hz) trong 180ms đầu, Nốt 2 G5 (783.99 Hz) trong 370ms sau
        int splitSample = (int)(sampleRate * 0.18);

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / sampleRate;
            double freq;
            double amplitude;

            if (i < splitSample)
            {
                freq = 523.25; // C5
                amplitude = Math.Exp(-4.0 * t);
            }
            else
            {
                freq = 783.99; // G5
                double t2 = t - 0.18;
                amplitude = Math.Exp(-4.5 * t2);
            }

            // Tạo sóng sin mượt mà với hài âm nhẹ cho âm thanh ấm và rõ ràng
            double sine1 = Math.Sin(2.0 * Math.PI * freq * t);
            double sine2 = 0.25 * Math.Sin(4.0 * Math.PI * freq * t);
            double wave = (sine1 + sine2) * amplitude * 24000.0;

            samples[i] = (short)Math.Clamp(wave, short.MinValue, short.MaxValue);
        }

        // Đóng gói thành file RIFF/WAV chuẩn
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // RIFF header
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + numSamples * 2);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        // fmt subchunk
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16); // Subchunk1Size
        writer.Write((short)1); // AudioFormat = 1 (PCM)
        writer.Write((short)1); // NumChannels = 1 (Mono)
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2); // ByteRate = SampleRate * NumChannels * BitsPerSample/8
        writer.Write((short)2); // BlockAlign = NumChannels * BitsPerSample/8
        writer.Write((short)16); // BitsPerSample = 16

        // data subchunk
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(numSamples * 2);

        foreach (var sample in samples)
        {
            writer.Write(sample);
        }

        return stream.ToArray();
    }
}
