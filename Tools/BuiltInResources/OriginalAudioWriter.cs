using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class OriginalAudioWriter
{
    internal sealed record Sound(string name, string title, double frequency, double endFrequency, double duration, double brightness, bool loop = false)
    {
        internal string directory => loop ? "Music" : "UI";
    }
    internal static IReadOnlyList<Sound> sounds { get; } = [
        new("Hover", "轻触悬停", 880, 1046, 0.035, 0.1), new("Confirm", "柔和确认", 523, 784, 0.12, 0.25),
        new("Cancel", "返回取消", 660, 330, 0.1, 0.15), new("Open", "打开页面", 392, 784, 0.13, 0.2),
        new("Close", "关闭页面", 784, 392, 0.12, 0.15), new("PageForward", "向前翻页", 420, 900, 0.07, 0.45),
        new("PageBack", "向后翻页", 900, 420, 0.07, 0.45), new("Notice", "轻声提示", 784, 1046, 0.18, 0.1),
        new("Error", "操作不可用", 220, 196, 0.16, 0.3), new("Save", "保存完成", 523, 1046, 0.22, 0.15),
        new("Load", "读取完成", 392, 784, 0.22, 0.15), new("Unlock", "收藏解锁", 659, 1318, 0.3, 0.3),
        new("Focus", "焦点移动", 740, 880, 0.04, 0.08), new("Select", "选择条目", 587, 880, 0.08, 0.18),
        new("ToggleOn", "启用选项", 440, 660, 0.085, 0.12), new("ToggleOff", "关闭选项", 660, 440, 0.085, 0.12),
        new("Slider", "调整滑块", 660, 698, 0.025, 0.08), new("Typing", "文字轻响", 1200, 900, 0.018, 0.3),
        new("Start", "故事开始", 392, 1046, 0.35, 0.2), new("End", "段落落幕", 784, 392, 0.35, 0.1),
        new("Dawn", "晨光 · 循环音乐底色", 130.8128, 164.8138, 8, 0.08, true),
        new("PaperRoom", "纸间 · 循环音乐底色", 146.8324, 174.6141, 8, 0.05, true),
        new("QuietCinema", "静夜 · 循环音乐底色", 110, 130.8128, 8, 0.12, true)
    ];
    internal static void WriteSources(string root)
    {
        foreach (Sound sound in sounds)
        {
            string path = Path.Combine(root, "Assets", "BuiltIn", "Audio", sound.directory, sound.name + ".wav");
            if (File.Exists(path)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, Encode(sound));
        }
    }
    private static byte[] Encode(Sound sound)
    {
        const int rate = 44100;
        int frames = (int)Math.Round(rate * sound.duration);
        using var data = new MemoryStream(44 + frames * 2);
        using var writer = new BinaryWriter(data, Encoding.UTF8, true);
        writer.Write("RIFF"u8); writer.Write(36 + frames * 2); writer.Write("WAVEfmt "u8); writer.Write(16);
        writer.Write((ushort)1); writer.Write((ushort)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((ushort)2); writer.Write((ushort)16);
        writer.Write("data"u8); writer.Write(frames * 2);
        for (int index = 0; index < frames; index++)
        {
            double time = (double)index / rate, progress = (double)index / Math.Max(1, frames - 1);
            double value;
            if (sound.loop)
            {
                double a = Math.Round(sound.frequency * sound.duration) / sound.duration;
                double b = Math.Round(sound.endFrequency * sound.duration) / sound.duration;
                double c = Math.Round(sound.frequency * 1.5 * sound.duration) / sound.duration;
                double slow = 0.75 + 0.15 * Math.Cos(2 * Math.PI * time / sound.duration);
                value = (Math.Sin(2 * Math.PI * a * time) * 0.3 + Math.Sin(2 * Math.PI * b * time) * 0.23
                    + Math.Sin(2 * Math.PI * c * time) * 0.18 + Math.Sin(2 * Math.PI * a * 2 * time) * sound.brightness) * slow;
            }
            else
            {
                double phase = 2 * Math.PI * (sound.frequency * time + (sound.endFrequency - sound.frequency) * time * time / (2 * sound.duration));
                double envelope = Math.Min(1, time / 0.003) * Math.Pow(1 - progress, 2.5);
                value = (Math.Sin(phase) * 0.65 + Math.Sin(phase * 2) * sound.brightness) * envelope;
            }
            writer.Write((short)Math.Round(Math.Clamp(value, -0.95, 0.95) * short.MaxValue));
        }
        writer.Flush(); return data.ToArray();
    }
}
