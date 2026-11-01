using System;
using System.Collections.Generic;
using System.Globalization;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class OriginalIconLibrary
{
    internal static IReadOnlyList<string> Write(string root)
    {
        var icons = new List<(string name, string[] paths)>();
        Add("play", "M 8 4 L 20 12 L 8 20 Z");
        Add("pause", "M 7 4 L 7 20 M 17 4 L 17 20");
        Add("stop", "M 5 5 L 19 5 L 19 19 L 5 19 Z");
        Add("next", "M 5 4 L 17 12 L 5 20 Z M 20 4 L 20 20");
        Add("previous", "M 19 4 L 7 12 L 19 20 Z M 4 4 L 4 20");
        Add("forward", "M 3 5 L 12 12 L 3 19 Z M 12 5 L 21 12 L 12 19 Z");
        Add("rewind", "M 21 5 L 12 12 L 21 19 Z M 12 5 L 3 12 L 12 19 Z");
        Add("skip", "M 4 6 L 12 12 L 4 18 Z M 12 6 L 20 12 L 12 18 Z M 22 6 L 22 18");
        Add("auto", "M 4 18 L 10 5 L 16 18 M 7 13 L 13 13 M 18 8 L 22 12 L 18 16");
        Add("history", "M 3 9 C 5 1 17 1 21 8 C 25 18 12 26 5 18 M 3 4 L 3 9 L 8 9 M 12 6 L 12 12 L 16 14");
        Add("save", "M 4 3 L 17 3 L 21 7 L 21 21 L 3 21 L 3 3 Z M 7 3 L 7 10 L 16 10 L 16 3 M 7 21 L 7 14 L 17 14 L 17 21");
        Add("load", "M 3 9 L 3 19 L 21 19 L 21 9 M 12 3 L 12 14 M 8 10 L 12 14 L 16 10");
        Add("quick-save", "M 3 4 L 14 4 M 3 4 L 3 21 L 21 21 L 21 12 M 7 21 L 7 14 L 15 14 L 15 21 M 18 2 L 15 8 L 19 8 L 16 14 L 23 6 L 19 6 Z");
        Add("quick-load", "M 3 10 L 3 20 L 21 20 L 21 13 M 8 9 L 12 13 L 16 9 M 12 3 L 12 13 M 20 2 L 17 7 L 21 7 L 18 12");
        Add("settings", "M 9 3 L 15 3 L 16 7 L 20 8 L 22 13 L 18 16 L 17 21 L 12 22 L 9 18 L 4 17 L 2 12 L 6 9 L 7 4 Z", Circle(12, 12, 3));
        Add("menu", "M 4 6 L 20 6 M 4 12 L 20 12 M 4 18 L 20 18");
        Add("close", "M 5 5 L 19 19 M 19 5 L 5 19");
        Add("check", "M 4 12 L 10 18 L 21 5");
        Add("plus", "M 12 4 L 12 20 M 4 12 L 20 12");
        Add("minus", "M 4 12 L 20 12");
        Add("chevron-left", "M 15 4 L 7 12 L 15 20");
        Add("chevron-right", "M 9 4 L 17 12 L 9 20");
        Add("chevron-up", "M 4 15 L 12 7 L 20 15");
        Add("chevron-down", "M 4 9 L 12 17 L 20 9");
        Add("arrow-left", "M 20 12 L 4 12 M 10 5 L 3 12 L 10 19");
        Add("arrow-right", "M 4 12 L 20 12 M 14 5 L 21 12 L 14 19");
        Add("arrow-up", "M 12 20 L 12 4 M 5 10 L 12 3 L 19 10");
        Add("arrow-down", "M 12 4 L 12 20 M 5 14 L 12 21 L 19 14");
        Add("home", "M 2 11 L 12 3 L 22 11 M 5 9 L 5 21 L 10 21 L 10 14 L 14 14 L 14 21 L 19 21 L 19 9");
        Add("search", Circle(10, 10, 7), "M 15 15 L 21 21");
        Add("filter", "M 3 4 L 21 4 L 14 12 L 14 20 L 10 18 L 10 12 Z");
        Add("sort", "M 5 3 L 5 21 M 2 17 L 5 21 L 8 17 M 11 5 L 21 5 M 11 11 L 18 11 M 11 17 L 15 17");
        Add("grid", "M 3 3 L 10 3 L 10 10 L 3 10 Z M 14 3 L 21 3 L 21 10 L 14 10 Z M 3 14 L 10 14 L 10 21 L 3 21 Z M 14 14 L 21 14 L 21 21 L 14 21 Z");
        Add("list", "M 8 5 L 21 5 M 8 12 L 21 12 M 8 19 L 21 19 M 3 5 L 4 5 M 3 12 L 4 12 M 3 19 L 4 19");
        Add("folder", "M 3 5 L 9 5 L 12 8 L 21 8 L 21 20 L 3 20 Z");
        Add("file", "M 5 2 L 14 2 L 20 8 L 20 22 L 5 22 Z M 14 2 L 14 8 L 20 8 M 8 13 L 16 13 M 8 17 L 16 17");
        Add("image", "M 3 3 L 21 3 L 21 21 L 3 21 Z M 3 18 L 10 10 L 15 15 L 18 12 L 21 16", Circle(16, 7, 1.5f));
        Add("music", "M 9 17 L 9 5 L 21 3 L 21 15 M 9 8 L 21 6", Circle(6, 18, 3), Circle(18, 16, 3));
        Add("voice", "M 8 5 C 8 0 16 0 16 5 L 16 12 C 16 17 8 17 8 12 Z M 5 11 C 5 22 19 22 19 11 M 12 19 L 12 23 M 8 23 L 16 23");
        Add("volume", "M 3 9 L 7 9 L 13 4 L 13 20 L 7 15 L 3 15 Z M 17 8 C 20 10 20 14 17 16 M 20 4 C 26 8 26 16 20 20");
        Add("volume-mute", "M 3 9 L 7 9 L 13 4 L 13 20 L 7 15 L 3 15 Z M 17 9 L 22 15 M 22 9 L 17 15");
        Add("headphones", "M 3 14 L 3 11 C 3 0 21 0 21 11 L 21 14 M 3 12 L 7 12 L 7 21 L 3 21 Z M 17 12 L 21 12 L 21 21 L 17 21 Z");
        Add("language", Circle(12, 12, 10), "M 2 12 L 22 12 M 12 2 C 5 7 5 17 12 22 C 19 17 19 7 12 2 M 4 6 L 20 6 M 4 18 L 20 18");
        Add("font", "M 3 20 L 10 3 L 17 20 M 6 13 L 14 13 M 18 11 L 23 11 M 20 8 L 20 20");
        Add("eye", "M 2 12 C 7 2 17 2 22 12 C 17 22 7 22 2 12 Z", Circle(12, 12, 3));
        Add("eye-off", "M 3 9 C 8 3 16 3 22 12 C 20 16 17 18 13 18 M 2 12 C 4 16 7 18 10 18 M 3 3 L 21 21");
        Add("lock", "M 7 10 L 7 7 C 7 0 17 0 17 7 L 17 10 M 4 10 L 20 10 L 20 22 L 4 22 Z M 12 14 L 12 18");
        Add("unlock", "M 8 10 L 8 7 C 8 0 18 0 18 7 M 4 10 L 20 10 L 20 22 L 4 22 Z M 12 14 L 12 18");
        Add("star", "M 12 2 L 15 8 L 22 9 L 17 14 L 18 22 L 12 18 L 6 22 L 7 14 L 2 9 L 9 8 Z");
        Add("heart", "M 12 21 C 9 18 1 13 2 7 C 3 1 10 1 12 6 C 14 1 21 1 22 7 C 23 13 15 18 12 21 Z");
        Add("bookmark", "M 5 3 L 19 3 L 19 22 L 12 17 L 5 22 Z");
        Add("info", Circle(12, 12, 10), "M 12 10 L 12 17 M 12 6 L 12 7");
        Add("warning", "M 12 2 L 23 21 L 1 21 Z M 12 8 L 12 14 M 12 17 L 12 18");
        Add("error", Circle(12, 12, 10), "M 8 8 L 16 16 M 16 8 L 8 16");
        Add("help", Circle(12, 12, 10), "M 8 8 C 8 3 17 4 16 9 C 16 12 12 11 12 15 M 12 18 L 12 19");
        Add("refresh", "M 20 9 C 16 0 3 3 3 12 C 3 22 18 25 21 15 M 20 3 L 20 9 L 14 9");
        Add("undo", "M 3 10 L 3 4 M 3 10 L 9 10 M 3 10 C 9 0 23 5 21 15 C 20 20 16 22 11 21");
        Add("redo", "M 21 10 L 21 4 M 21 10 L 15 10 M 21 10 C 15 0 1 5 3 15 C 4 20 8 22 13 21");
        Add("copy", "M 8 8 L 21 8 L 21 21 L 8 21 Z M 16 4 L 16 2 L 2 2 L 2 16 L 4 16");
        Add("trash", "M 3 6 L 21 6 M 9 6 L 9 2 L 15 2 L 15 6 M 5 6 L 6 22 L 18 22 L 19 6 M 10 10 L 10 18 M 14 10 L 14 18");
        Add("edit", "M 3 21 L 4 15 L 17 2 L 22 7 L 9 20 Z M 14 5 L 19 10 M 4 15 L 9 20");
        Add("link", "M 10 6 L 13 3 C 19 -2 26 5 21 10 L 17 14 M 14 18 L 11 21 C 5 26 -2 19 3 14 L 7 10 M 8 16 L 16 8");
        Add("external", "M 14 3 L 22 3 L 22 11 M 22 3 L 10 15 M 11 4 L 3 4 L 3 22 L 21 22 L 21 14");
        Add("fullscreen", "M 3 9 L 3 3 L 9 3 M 15 3 L 21 3 L 21 9 M 21 15 L 21 21 L 15 21 M 9 21 L 3 21 L 3 15");
        Add("window", "M 3 4 L 21 4 L 21 21 L 3 21 Z M 3 9 L 21 9 M 6 6 L 7 6 M 10 6 L 11 6");
        Add("controller", "M 7 7 L 17 7 C 21 7 22 14 22 18 C 22 23 18 22 15 17 L 9 17 C 6 22 2 23 2 18 C 2 14 3 7 7 7 Z M 7 10 L 7 15 M 4 12 L 10 12 M 16 11 L 17 11 M 19 14 L 20 14");
        Add("keyboard", "M 2 5 L 22 5 L 22 20 L 2 20 Z M 5 9 L 7 9 M 10 9 L 12 9 M 15 9 L 17 9 M 5 13 L 7 13 M 10 13 L 12 13 M 15 13 L 19 13 M 7 17 L 17 17");
        Add("mouse", "M 5 10 C 5 -1 19 -1 19 10 L 19 15 C 19 26 5 26 5 15 Z M 12 2 L 12 10 M 5 10 L 19 10");
        foreach (var icon in icons)
        {
            using var canvas = new ArtCanvas(128, 128, 24, 24);
            foreach (string path in icon.paths) { using var shape = Parse(path); canvas.Draw(shape, null, 0xFFFFFFFF, 1.6f); }
            canvas.Save(root, "BuiltIn/Icons/" + icon.name + ".png");
        }
        return icons.ConvertAll(static icon => icon.name).AsReadOnly();
        void Add(string name, params string[] paths) => icons.Add((name, paths));
    }
    private static ArtPath Parse(string path)
    {
        var result = new ArtPath(); string[] tokens = path.Split(' ', StringSplitOptions.RemoveEmptyEntries); int cursor = 0;
        while (cursor < tokens.Length)
        {
            switch (tokens[cursor++])
            {
                case "M": result.M(Number(), Number()); break;
                case "L": result.L(Number(), Number()); break;
                case "C": result.C(Number(), Number(), Number(), Number(), Number(), Number()); break;
                case "Z": result.Z(); break;
                default: result.Dispose(); throw new InvalidOperationException("Unsupported original icon path command.");
            }
        }
        return result;
        float Number() => float.Parse(tokens[cursor++], CultureInfo.InvariantCulture);
    }
    private static string Circle(
        float x,
        float y,
        float radius
    ) {
        float k = radius * .55228475f;
        return FormattableString.Invariant($"M {x + radius} {y} C {x + radius} {y + k} {x + k} {y + radius} {x} {y + radius} C {x - k} {y + radius} {x - radius} {y + k} {x - radius} {y} C {x - radius} {y - k} {x - k} {y - radius} {x} {y - radius} C {x + k} {y - radius} {x + radius} {y - k} {x + radius} {y} Z");
    }
}
