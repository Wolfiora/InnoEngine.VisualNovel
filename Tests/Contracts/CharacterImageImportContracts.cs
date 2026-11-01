using System;
using System.IO;
using System.Linq;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Authoring.CharacterEditing.ImageImport;
using Inno.VisualNovel.UI.Imaging;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class CharacterImageImportContracts
{
    internal static void Run(
        string root,
        AssetPipeline assets,
        CharacterAsset imported
    ) {
        string directory = Path.Combine(root, "Assets", "Batch"); Directory.CreateDirectory(directory);
        string posePath = Path.Combine(directory, "pose__挥手__身体.png");
        File.WriteAllBytes(posePath, Png(2, 3, 96));
        File.WriteAllBytes(Path.Combine(directory, "expression__微笑__脸.png"), Png(3, 2, 160));
        File.WriteAllBytes(Path.Combine(directory, "unmatched.png"), Png(1, 1, 224));
        var source = CharacterAssetCopy.Copy(imported);
        source.poses[0].name = "站立"; source.poses[0].parts[0].name = "身体"; source.poses[0].parts[1].name = "脸";
        source.poses[0].parts[0].transform.pivotX = 8; source.poses[0].parts[0].surface.cropWidth = .5;
        CharacterImageBatchInput[] inputs;
        using (var preparation = new NativeCharacterImagePreparation(root, "Batch"))
        {
            preparation.Step(1);
            Check(preparation.processed == 1 && !preparation.finished, "Native preparation ignored its per-call image budget.");
            while (!preparation.finished) preparation.Step(1);
            Check(preparation.error.Length == 0 && preparation.total == 3, "Finite native image preparation failed.");
            inputs = preparation.inputs.ToArray();
        }
        Check(inputs.All(static item => item.image is not null && item.error.Length == 0), "Prepared image references were unavailable.");
        var disabled = CharacterImageBatchService.Preview(source, inputs);
        Check(disabled.readyCount == 0, "Import guessed permission to create unmatched variants.");
        var review = CharacterImageBatchService.Preview(source, inputs, true);
        Check(review.readyCount == 2 && review.rows.Count == 3 && source.poses.Length == 1 && source.expressions.Length == 2,
            "Filename review changed the live source or discarded an unmatched image.");
        var display = review.CopyForDisplay();
        var pose = display.poses.Single(item => item.name == "挥手");
        Check(pose.parts[0].width == 100 && pose.parts[0].transform.pivotX == 8 && pose.parts[0].surface.cropWidth == .5
            && display.expressions.Single(item => item.name == "微笑").parts.Single().partId == "face", "Candidate lost inherited geometry or semantic expression ownership.");
        display.poses = []; display.displayName = "Forged external display copy";
        var stale = CharacterAssetCopy.Copy(source); stale.displayName = "Changed source";
        Reject(() => CharacterImageBatchService.ApplyReady(stale, review), "Review accepted a changed source baseline.");
        Check(stale.poses.Length == 1 && stale.displayName == "Changed source", "Rejected review partially replaced source values.");
        var applied = CharacterAssetCopy.Copy(source); CharacterImageBatchService.ApplyReady(applied, review);
        Check(applied.displayName == source.displayName && applied.poses.Length == 2 && applied.expressions.Length == 3
            && applied.poses.Single(item => item.name == "挥手").id == pose.id, "Display mutations forged the private review or the reviewed stable IDs changed during apply.");
        Check(EditorAssets.Save(AssetPath.Project("BatchActor.ivncharacter"), applied), "Reviewed character failed native import/save.");
        var native = Inno.Assets.Assets.Load<CharacterAsset>(AssetPath.Project("BatchActor.ivncharacter"));
        Check(native.poses.Single(item => item.name == "挥手").parts[0].name == "身体"
            && EditorAssets.CaptureProperties(native).dependencies.Any(item => item.persistentId == review.rows.Single(item => item.kind == CharacterImageBatchKind.Expression && item.applicable).imageId),
            "Native character source lost readable part labels or the batch image dependency.");
        File.WriteAllBytes(posePath, Png(4, 2, 48));
        Check(assets.Import(AssetPath.Project("Batch/pose__挥手__身体.png")), "Changed native batch image could not be reimported.");
        var untouched = CharacterAssetCopy.Copy(source);
        Reject(() => CharacterImageBatchService.ApplyReady(untouched, review), "Review accepted a changed native content generation.");
        Check(untouched.poses.Length == 1 && untouched.expressions.Length == 2, "Image-generation rejection partially applied another ready row.");
        File.WriteAllBytes(Path.Combine(directory, "pose__挥手__body.png"), Png(2, 2, 80));
        using var duplicates = new NativeCharacterImagePreparation(root, "Batch");
        while (!duplicates.finished) duplicates.Step();
        var conflicting = CharacterImageBatchService.Preview(source, duplicates.inputs, true, true);
        Check(conflicting.readyCount == 1 && conflicting.rows.Count(item => item.kind == CharacterImageBatchKind.Pose && !item.applicable) == 2
            && conflicting.CopyForDisplay().poses.Length == 1, "Duplicate semantic targets chose a winner or created an empty pose.");
        using var cancelled = new NativeCharacterImagePreparation(root, "Batch"); cancelled.Step(1); cancelled.Dispose();
        Reject(() => cancelled.Step(), "Disposed preparation continued importing resources.");
        Reject(() => new NativeCharacterImagePreparation(root, ""), "Batch preparation scanned the entire asset root.");
    }
    private static byte[] Png(int width, int height, byte value)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int index = 0; index < pixels.Length; index += 4) { pixels[index] = value; pixels[index + 1] = 96; pixels[index + 2] = 128; pixels[index + 3] = 255; }
        return UiPngImage.Encode(new(width, height, pixels));
    }
    private static void Reject(Action action, string message)
    { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } Check(rejected, message); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
