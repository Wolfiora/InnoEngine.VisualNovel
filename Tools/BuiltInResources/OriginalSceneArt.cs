using System;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class OriginalSceneArt
{
    internal static readonly string[] names = ["Classroom", "Station", "Seafront", "Woodland", "Bedroom", "Rooftop"];
    internal static void Write(string root)
    {
        foreach (string name in names)
        {
            using var canvas = new ArtCanvas(1920, 1080);
            canvas.Gradient(0x97C7CBFF, 0xE8E5CEFF, 0, 0, 1920, 1080);
            if (name == "Classroom") Classroom(canvas);
            else if (name == "Bedroom") Bedroom(canvas);
            else if (name == "Woodland") Woodland(canvas);
            else
            {
                Sky(canvas, name == "Station");
                if (name == "Seafront") Seafront(canvas);
                else if (name == "Station") Station(canvas);
                else Rooftop(canvas);
            }
            canvas.Save(root, "BuiltIn/Backgrounds/" + name + ".png");
        }
    }
    private static void Sky(
        ArtCanvas canvas,
        bool sunset
    ) {
        canvas.Gradient(sunset ? 0x8797B7FF : 0x8CBFD1FF, sunset ? 0xF0C5A4FF : 0xEEE9D0FF, 0, 0, 1920, 780);
        canvas.Ellipse(0xFFF6DDE0, sunset ? 1410 : 1430, sunset ? 410 : 100, 200, 200);
        foreach (var cloud in new[] { (160f, 160f, 260f), (620f, 90f, 180f), (1110f, 250f, 230f) })
        {
            canvas.Ellipse(0xFFF7E84A, cloud.Item1, cloud.Item2, cloud.Item3, 36);
            canvas.Ellipse(0xFFF7E84A, cloud.Item1 + 80, cloud.Item2 - 14, cloud.Item3, 40);
        }
        for (int index = 0; index < 29; index++)
        {
            int height = 60 + index * 47 % 170;
            canvas.Rect(0x738D9D75, index * 75 - 40, 700 - height, 66, height + 60);
            if (index % 3 == 0) canvas.Rect(0x738D9D75, index * 75 - 16, 680 - height, 5, 35);
        }
    }
    private static void Seafront(ArtCanvas canvas)
    {
        canvas.Gradient(0x6EABB3FF, 0x365D78FF, 0, 650, 1920, 430);
        for (int row = 0; row < 13; row++) for (int index = 0; index < 10; index++)
            canvas.Rect(0xDCF0DF65, (index * 239 + row * 91) % 1920, 680 + row * row * 1.8f, 55 + row * 7, 2 + row * .3f);
        Poly(canvas, 0xE5DCC5FF, 0, 850, 640, 770, 1920, 1040, 1920, 1080, 0, 1080);
        Poly(canvas, 0x9A9993FF, 0, 830, 650, 750, 1920, 1010, 1920, 1040, 640, 780, 0, 860);
        for (int index = 0; index < 10; index++)
        {
            float x = index * 220, y = 810 - Math.Min(x, 650) * .12f + Math.Max(0, x - 650) * .205f;
            canvas.Rect(0x3D5560FF, x, y - 145, 10, 170);
        }
        Line(canvas, 0x3D5560FF, 10, 0, 670, 650, 600, 1920, 860);
        Line(canvas, 0x3D5560FF, 5, 0, 730, 650, 660, 1920, 920);
        Tree(canvas, 100, 660, 1.2f, 0x577F7BFF);
        canvas.Rect(0x233C4AFF, 1740, 330, 10, 690); canvas.Rect(0x233C4AFF, 1690, 320, 110, 14); canvas.Ellipse(0xFFE9B6FF, 1700, 335, 90, 34);
    }
    private static void Station(ArtCanvas canvas)
    {
        canvas.Rect(0x4B6371FF, 0, 720, 1920, 360);
        Poly(canvas, 0xD2C5B0FF, 0, 720, 1920, 720, 1540, 1080, 0, 1080);
        Poly(canvas, 0xEDD7A4FF, 0, 820, 1820, 820, 1795, 844, 0, 844);
        for (int index = 0; index < 12; index++) Line(canvas, 0xA69D93FF, 2, index * 180, 720, index * 240 - 220, 1080);
        for (int index = 0; index < 4; index++)
        {
            float x = 80 + index * 490;
            canvas.Rect(0x425363FF, x, 230, 34, 510); canvas.Rect(0xD5CFB8FF, x + 6, 250, 6, 450);
        }
        Poly(canvas, 0x314553FF, 0, 120, 1920, 120, 1920, 230, 0, 310);
        Poly(canvas, 0x6C8990FF, 0, 120, 1920, 120, 1920, 150, 0, 230);
        canvas.Rect(0xF2E6C1FF, 760, 288, 350, 110); canvas.Rect(0x375565FF, 780, 305, 310, 76);
        canvas.Rect(0xCBD7D0FF, 820, 325, 190, 12); canvas.Rect(0xCBD7D0FF, 820, 350, 140, 7);
        canvas.Rect(0x567182FF, 280, 635, 380, 26); canvas.Rect(0x567182FF, 280, 575, 380, 20);
        canvas.Rect(0x3D5362FF, 310, 655, 12, 70); canvas.Rect(0x3D5362FF, 610, 655, 12, 70);
        Line(canvas, 0x3E5262FF, 5, 0, 420, 1920, 430); Line(canvas, 0x3E5262FF, 3, 0, 470, 1920, 450);
    }
    private static void Rooftop(ArtCanvas canvas)
    {
        canvas.Rect(0x81979AFF, 0, 770, 1920, 310);
        Poly(canvas, 0xB8C2BBFF, 0, 815, 1920, 815, 1920, 1080, 0, 1080);
        for (int index = 0; index < 14; index++) Line(canvas, 0x8C9E9BFF, 2, index * 160, 815, index * 210 - 400, 1080);
        for (int row = 0; row < 5; row++) canvas.Rect(0x8C9E9BFF, 0, 825 + row * row * 15, 1920, 2);
        canvas.Rect(0x526B73FF, 0, 760, 1920, 14); canvas.Rect(0x526B73FF, 0, 430, 1920, 10);
        for (int index = 0; index < 25; index++)
        {
            canvas.Rect(0x526B73FF, index * 80, 430, 6, 340);
            Line(canvas, 0x526B7345, 1, index * 80, 440, index * 80 + 240, 760);
        }
        canvas.Rect(0xD5D6C4FF, 0, 310, 440, 505); canvas.Rect(0x8BACA9FF, 82, 415, 240, 400);
        canvas.Rect(0x496674FF, 104, 444, 196, 180); canvas.Rect(0xD6CDA9FF, 288, 666, 10, 10);
        canvas.Rect(0x7A9498FF, 0, 285, 470, 34);
    }
    private static void Woodland(ArtCanvas canvas)
    {
        canvas.Gradient(0xBCD4B7FF, 0xE7DBB9FF, 0, 0, 1920, 1080);
        for (int index = 0; index < 18; index++)
        {
            float x = index * 130 - 100, top = (index * 73 % 180) - 80;
            canvas.Rect(0x78978B70, x, 40, 25 + index % 4 * 9, 740);
            canvas.Ellipse(0x6C9B8580, x - 140, top, 380, 300);
        }
        Poly(canvas, 0x769985FF, 0, 670, 1920, 670, 1920, 1080, 0, 1080);
        Poly(canvas, 0xDBCFADFF, 930, 670, 1050, 670, 1450, 1080, 420, 1080);
        for (int index = 0; index < 9; index++) Tree(canvas, index < 5 ? index * 200 - 100 : 1240 + (index - 5) * 220, 850 + index % 2 * 160, 1.4f + index % 3 * .2f, 0x416F66FF);
        for (int index = 0; index < 80; index++)
        {
            float x = index * 773 % 1920, y = 680 + index * 37 % 380;
            if (x > 650 && x < 1250) continue;
            canvas.Ellipse(index % 2 == 0 ? 0xE9E1B2FF : 0xA6BF8BFF, x, y, 8, 4);
        }
    }
    private static void Classroom(ArtCanvas canvas)
    {
        canvas.Rect(0xE3DDC8FF, 0, 0, 1920, 1080); canvas.Rect(0x9CB8ACFF, 0, 650, 1920, 430);
        Poly(canvas, 0xA4B1AAFF, 0, 720, 1920, 650, 1920, 1080, 0, 1080);
        canvas.Rect(0xF3EBD2FF, 120, 100, 750, 515);
        for (int index = 0; index < 3; index++)
        {
            int x = 145 + index * 245;
            canvas.Gradient(0x85B8CBFF, 0xE1EAD9FF, x, 130, 215, 450);
            canvas.Rect(0xBBD4C6FF, x, 400, 215, 180); canvas.Rect(0xF3EBD2FF, x, 350, 215, 14);
            canvas.Rect(0xF3EBD2FF, x + 100, 130, 14, 450);
        }
        canvas.Rect(0x6C847CFF, 1050, 170, 680, 370); canvas.Rect(0x3B625EFF, 1070, 190, 640, 330);
        canvas.Rect(0xE4DEC3B0, 1110, 245, 280, 3); canvas.Rect(0xE4DEC370, 1110, 285, 420, 3); canvas.Rect(0xE4DEC370, 1110, 325, 340, 3);
        canvas.Rect(0xA88564FF, 1030, 535, 720, 16); canvas.Rect(0xEEE9D2FF, 0, 50, 1920, 16);
        for (int row = 0; row < 3; row++) for (int column = 0; column < 4; column++)
        {
            float y = 665 + row * 145, x = 80 + column * 450 - row * 80, width = 260 + row * 50;
            Poly(canvas, 0xC6A67CFF, x, y, x + width, y - 18, x + width + 50, y + 45, x - 30, y + 62);
            Line(canvas, 0x51656AFF, 10, x, y + 60, x - 8, y + 180); Line(canvas, 0x51656AFF, 10, x + width + 20, y + 45, x + width + 34, y + 160);
            canvas.Rect(0x6D8C8AFF, x + 40, y + 95, width - 50, 14);
        }
        Tree(canvas, 1800, 650, .7f, 0x6B947CFF);
    }
    private static void Bedroom(ArtCanvas canvas)
    {
        canvas.Rect(0xE8DFCBFF, 0, 0, 1920, 1080); canvas.Rect(0xBCA28AFF, 0, 860, 1920, 220);
        for (int index = 0; index < 10; index++) canvas.Rect(0xA78F79FF, index * 220, 860, 2, 220);
        canvas.Rect(0x758F99FF, 1050, 100, 600, 500); canvas.Gradient(0x7CAABDFF, 0xD8DDD1FF, 1075, 125, 550, 450);
        canvas.Rect(0xECE2CAFF, 1338, 125, 18, 450); canvas.Rect(0xECE2CAFF, 1075, 335, 550, 16);
        canvas.Rect(0xA0B8ACD0, 990, 80, 105, 580); canvas.Rect(0xA0B8ACD0, 1600, 80, 120, 580);
        canvas.Rect(0x847B6FFF, 50, 300, 470, 570);
        for (int shelf = 0; shelf < 4; shelf++)
        {
            canvas.Rect(0xC3AC88FF, 70, 370 + shelf * 130, 430, 15);
            for (int book = 0; book < 9; book++)
            { int height = 55 + book * 17 % 50; canvas.Rect(book % 3 == 0 ? 0x81A6A7FF : book % 3 == 1 ? 0xD7C395FF : 0xA87F78FF, 85 + book * 44, 370 + shelf * 130 - height, 28, height); }
        }
        Poly(canvas, 0x849F9DFF, 550, 650, 1100, 650, 1450, 1000, 400, 1000); canvas.Rect(0x546C72FF, 400, 990, 1050, 55);
        Poly(canvas, 0xD3D9C3FF, 550, 650, 1100, 650, 1230, 790, 490, 790);
        Poly(canvas, 0xEFE6CBFF, 610, 630, 930, 630, 1010, 710, 555, 710);
        Line(canvas, 0xAAC0B4FF, 5, 430, 960, 1410, 960);
        canvas.Rect(0xA58C6FFF, 1500, 730, 360, 20); canvas.Rect(0xA58C6FFF, 1530, 750, 15, 210); canvas.Rect(0xA58C6FFF, 1810, 750, 15, 210);
        canvas.Rect(0x5D7C82FF, 1640, 570, 12, 160); Poly(canvas, 0xE8D4A8FF, 1590, 560, 1700, 560, 1740, 640, 1550, 640);
        canvas.Rect(0xF5EBD2FF, 650, 130, 250, 300); canvas.Rect(0x88ADA6FF, 675, 155, 200, 250);
        canvas.Ellipse(0xE5CF9FFF, 735, 185, 70, 70); Poly(canvas, 0x557D78FF, 675, 405, 760, 285, 875, 405);
    }
    private static void Tree(
        ArtCanvas canvas,
        float x,
        float y,
        float scale,
        uint leaves
    ) {
        canvas.Rect(0x4B665FFF, x, y - 320 * scale, 26 * scale, 340 * scale);
        canvas.Ellipse(leaves, x - 130 * scale, y - 540 * scale, 300 * scale, 260 * scale);
        canvas.Ellipse(leaves, x - 190 * scale, y - 390 * scale, 370 * scale, 220 * scale);
        canvas.Ellipse(0x93B49B45, x - 110 * scale, y - 510 * scale, 190 * scale, 150 * scale);
    }
    private static void Poly(
        ArtCanvas canvas,
        uint color,
        params float[] coordinates
    ) {
        using var path = new ArtPath(); path.M(coordinates[0], coordinates[1]);
        for (int index = 2; index < coordinates.Length; index += 2) path.L(coordinates[index], coordinates[index + 1]);
        canvas.Draw(path.Z(), color);
    }
    private static void Line(
        ArtCanvas canvas,
        uint color,
        float width,
        params float[] coordinates
    ) {
        using var path = new ArtPath(); path.M(coordinates[0], coordinates[1]);
        for (int index = 2; index < coordinates.Length; index += 2) path.L(coordinates[index], coordinates[index + 1]);
        canvas.Draw(path, null, color, width);
    }
}
