using System;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class OriginalCharacterArt
{
    internal static readonly string[] names = ["Cheng", "Shu"];
    internal static readonly string[] expressions = ["Neutral", "Smile", "Sad", "Angry", "Surprised", "Shy"];
    internal static void Write(string root)
    {
        foreach (string name in names)
        {
            WritePart("HairBack", canvas => HairBack(canvas, name));
            WritePart("BodyIdle", canvas => Body(canvas, name, false));
            WritePart("BodyWave", canvas => Body(canvas, name, true));
            WritePart("Face", canvas => Face(canvas, name));
            WritePart("HairFront", canvas => HairFront(canvas, name));
            foreach (string expression in expressions) WritePart(expression, canvas => Features(canvas, name, expression));
            WritePart("Portrait", canvas => { HairBack(canvas, name); Body(canvas, name, false); Face(canvas, name); Features(canvas, name, "Neutral"); HairFront(canvas, name); });
            using (var avatar = new ArtCanvas(320, 400))
            {
                avatar.Translate(-140, -50); HairBack(avatar, name); Face(avatar, name); Features(avatar, name, "Neutral"); HairFront(avatar, name);
                avatar.Save(root, "BuiltIn/Characters/" + name + "/Avatar.png");
            }
            void WritePart(string part, Action<ArtCanvas> draw)
            {
                using var canvas = new ArtCanvas(768, 1152, 600, 900); draw(canvas);
                canvas.Save(root, "BuiltIn/Characters/" + name + "/" + part + ".png");
            }
        }
    }
    private static uint Skin(string name) => name == "Cheng" ? 0xF2D3BBFF : 0xDDB99EFF;
    private static uint Hair(string name) => name == "Cheng" ? 0x304957FFu : 0x564D49FFu;
    private static uint Ink(string name) => name == "Cheng" ? 0x273F4FFFu : 0x49443FFFu;
    private static void HairBack(
        ArtCanvas canvas,
        string name
    ) {
        using var path = new ArtPath();
        if (name == "Cheng") path.M(167, 210).C(155, 40, 398, 26, 428, 184).L(439, 409).C(364, 454, 232, 449, 158, 396).Z();
        else path.M(180, 225).C(130, 90, 255, 48, 312, 70).C(394, 38, 465, 162, 411, 263).L(190, 286).Z();
        canvas.Draw(path, Hair(name), Ink(name), 3);
    }
    private static void Body(
        ArtCanvas canvas,
        string name,
        bool wave
    ) {
        uint outer = name == "Cheng" ? 0x567B8EFF : 0x8EA99CFF, light = name == "Cheng" ? 0xE9E7D6FF : 0xD8DAC2FF, dark = Ink(name);
        canvas.Rect(Skin(name), 267, 340, 66, 103);
        using (var back = new ArtPath())
        {
            back.M(225, 402).C(180, 418, 142, 433, 125, 495).L(85, 855).L(178, 880).L(202, 610).L(200, 900).L(412, 900).L(409, 610);
            if (!wave) back.L(448, 867).L(526, 850).L(473, 493).C(465, 445, 407, 411, 364, 403);
            else back.L(455, 573).C(486, 513, 506, 412, 511, 344).L(446, 327).C(437, 400, 407, 464, 390, 476).L(367, 407);
            back.C(329, 428, 262, 430, 225, 402).Z(); canvas.Draw(back, outer, dark, 3);
        }
        using (var shirt = new ArtPath())
        {
            shirt.M(249, 414).C(266, 449, 329, 449, 347, 414).L(367, 900).L(235, 900).Z(); canvas.Draw(shirt, light);
        }
        if (name == "Cheng")
        {
            using var collar = new ArtPath(); collar.M(232, 411).L(282, 463).L(237, 527).L(208, 437).Z().M(366, 411).L(316, 463).L(363, 527).L(393, 437).Z(); canvas.Draw(collar, 0xDBE0D4FF, dark, 2);
            canvas.Rect(0x375C73FF, 278, 523, 48, 280);
            canvas.Ellipse(0xD8C298FF, 221, 572, 8, 8); canvas.Ellipse(0xD8C298FF, 221, 677, 8, 8);
            using var pocket = new ArtPath(); pocket.M(362, 625).L(405, 625).L(401, 672).L(365, 672).Z(); canvas.Draw(pocket, 0x41697FFF, dark, 2);
        }
        else
        {
            using var hood = new ArtPath(); hood.M(223, 409).C(191, 456, 229, 485, 277, 485).L(300, 459).L(321, 485).C(373, 485, 410, 450, 367, 407).L(341, 434).C(309, 453, 275, 452, 250, 433).Z(); canvas.Draw(hood, 0xB8C6B2FF, dark, 2);
            using var pocket = new ArtPath(); pocket.M(244, 692).L(356, 692).L(380, 770).L(221, 770).Z(); canvas.Draw(pocket, 0x78988CFF, dark, 2);
            using var cord = new ArtPath(); cord.M(270, 480).L(263, 596).M(330, 480).L(337, 596); canvas.Draw(cord, null, light, 4);
        }
        if (wave)
        {
            using var hand = new ArtPath(); hand.M(449, 341).L(444, 278).C(444, 258, 458, 261, 460, 275).L(465, 305).L(463, 242).C(463, 225, 477, 224, 480, 242).L(484, 298).L(491, 238).C(494, 223, 506, 228, 506, 243).L(504, 303).L(516, 258).C(521, 245, 533, 252, 529, 267).L(515, 332).C(503, 365, 473, 367, 449, 341).Z(); canvas.Draw(hand, Skin(name), dark, 2.5f);
        }
        else
        {
            canvas.Ellipse(Skin(name), 442, 846, 60, 73); canvas.Ellipse(Skin(name), 98, 850, 57, 70);
        }
        using var sleeve = new ArtPath(); sleeve.M(151, 561).L(121, 809).M(421, 563).L(wave ? 472 : 478, wave ? 420 : 814); canvas.Draw(sleeve, null, dark, 2);
    }
    private static void Face(
        ArtCanvas canvas,
        string name
    ) {
        canvas.Ellipse(Skin(name), 181, 225, 39, 63); canvas.Ellipse(Skin(name), 384, 225, 39, 63);
        using var face = new ArtPath(); face.M(197, 184).C(194, 119, 405, 117, 405, 191).L(397, 283).C(391, 345, 336, 385, 302, 391).C(260, 384, 210, 344, 202, 280).Z(); canvas.Draw(face, Skin(name), Ink(name), 2.2f);
        using var nose = new ArtPath(); nose.M(300, 260).L(293, 284).L(303, 287); canvas.Draw(nose, null, name == "Cheng" ? 0xC5A792FF : 0xB48E78FF, 2);
        canvas.Ellipse(0xF8E1CC55, 228, 276, 30, 55);
    }
    private static void HairFront(
        ArtCanvas canvas,
        string name
    ) {
        using var path = new ArtPath();
        if (name == "Cheng")
            path.M(174, 211).C(163, 103, 221, 66, 294, 79).C(377, 48, 430, 122, 426, 225).L(400, 288).L(389, 180).L(368, 224).L(351, 171).L(326, 212).L(307, 156).C(278, 186, 244, 216, 212, 226).L(203, 288).L(181, 258).Z();
        else path.M(178, 208).L(169, 165).L(183, 153).L(164, 131).L(195, 126).C(202, 71, 263, 65, 295, 76).C(364, 38, 423, 101, 430, 172).L(416, 245).L(395, 262).L(395, 180).L(376, 203).L(348, 164).L(333, 201).L(310, 151).C(282, 192, 233, 213, 206, 212).L(202, 259).L(183, 240).Z();
        canvas.Draw(path, Hair(name), Ink(name), 3);
        using var highlight = new ArtPath(); highlight.M(207, 158).C(217, 119, 252, 102, 289, 108).M(325, 106).C(367, 100, 393, 129, 403, 158); canvas.Draw(highlight, null, name == "Cheng" ? 0x5E7D86FFu : 0x7E7164FFu, 5);
        if (name == "Cheng")
        {
            using var pin = new ArtPath(); pin.M(381, 198).L(402, 180).M(385, 207).L(407, 189); canvas.Draw(pin, null, 0xE2C293FF, 5);
        }
    }
    private static void Features(
        ArtCanvas canvas,
        string name,
        string expression
    ) {
        uint ink = Ink(name); bool smile = expression is "Smile" or "Shy", surprise = expression == "Surprised", sad = expression == "Sad", angry = expression == "Angry";
        foreach (int center in new[] { 246, 354 })
        {
            bool left = center < 300;
            using var brow = new ArtPath(); float near = angry ? 229 : sad ? 207 : 212, far = angry ? 212 : sad ? 223 : 213;
            brow.M(center - 24, left ? far : near).C(center - 5, left ? far - 5 : near - 5, center + 5, left ? near - 5 : far - 5, center + 24, left ? near : far); canvas.Draw(brow, null, ink, 4);
            if (smile)
            { using var eye = new ArtPath(); eye.M(center - 23, 249).C(center - 10, 230, center + 9, 230, center + 23, 249); canvas.Draw(eye, null, ink, 4); }
            else
            {
                using var eye = new ArtPath(); eye.M(center - 25, 244).C(center - 10, surprise ? 217 : 229, center + 11, surprise ? 217 : 229, center + 24, 243).C(center + 7, surprise ? 272 : 258, center - 10, surprise ? 272 : 258, center - 25, 244).Z(); canvas.Draw(eye, 0xF4EFE2FF, ink, 2.4f);
                canvas.Ellipse(name == "Cheng" ? 0x648D92FF : 0x8A8B69FF, center - 10, 231, 21, surprise ? 28 : 24); canvas.Ellipse(ink, center - 5, 234, 11, 17); canvas.Ellipse(0xFFF8E8FF, center - 5, 234, 5, 6);
            }
        }
        if (smile || expression == "Shy")
        { canvas.Ellipse(0xD68D8555, 210, 271, 42, 18); canvas.Ellipse(0xD68D8555, 352, 271, 42, 18); }
        using var mouth = new ArtPath();
        if (surprise) { canvas.Ellipse(0x9B6E66FF, 289, 307, 23, 27); return; }
        if (expression == "Smile") mouth.M(276, 305).C(288, 331, 314, 331, 326, 305).Z();
        else if (sad || angry) mouth.M(281, 322).C(295, 312, 309, 312, 321, 321);
        else if (expression == "Shy") mouth.M(287, 314).C(297, 322, 309, 321, 316, 313);
        else mouth.M(284, 314).C(294, 318, 308, 318, 319, 313);
        canvas.Draw(mouth, expression == "Smile" ? 0xA5746DFF : null, 0x9E7267FF, 2.5f);
    }
}
