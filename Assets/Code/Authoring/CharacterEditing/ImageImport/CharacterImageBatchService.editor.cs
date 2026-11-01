using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using InnoEngine.Rendering;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.CharacterEditing.ImageImport;

/// <summary>Matches explicit native image filenames and prepares a frozen, conflict-free character edit.</summary>
public static class CharacterImageBatchService
{
    /// <summary>Matches pose__variant__part or expression__variant__part filenames by stable identity or unambiguous display name.</summary>
    /// <param name="source">The exact current character draft.</param>
    /// <param name="inputs">At most 512 explicitly prepared native image inputs.</param>
    /// <param name="createVariants">Whether unmatched named variants may be created from the default pose or inherited expression.</param>
    /// <param name="resize">Whether replacement pose parts adopt imported image dimensions.</param>
    /// <returns>An immutable review whose ready rows form one atomic candidate.</returns>
    public static CharacterImageBatchPreview Preview(
        CharacterAsset source,
        IEnumerable<CharacterImageBatchInput> inputs,
        bool createVariants = false,
        bool resize = false
    ) {
        CharacterDefinitionRules.Validate(source); CharacterAnimationRules.Validate(source);
        if (!source.poses.Any(item => item.id == source.defaultPose)) throw new InvalidOperationException("Choose a valid default pose before matching character images.");
        var incoming = inputs.Take(513).OrderBy(static item => item.path.ToString(), StringComparer.Ordinal).ToArray();
        if (incoming.Length > 512) throw new InvalidOperationException("Prepare at most 512 character images in one batch.");
        string baseline = Fingerprint(source);
        var groups = new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<(CharacterImageBatchRow row, TextureAsset? image)>();
        foreach (var input in incoming)
        {
            string[] tokens = Path.GetFileNameWithoutExtension(input.path.localPath).Split("__", StringSplitOptions.None);
            CharacterImageBatchKind kind = tokens.Length == 3 && string.Equals(tokens[0], "pose", StringComparison.OrdinalIgnoreCase) ? CharacterImageBatchKind.Pose
                : tokens.Length == 3 && string.Equals(tokens[0], "expression", StringComparison.OrdinalIgnoreCase) ? CharacterImageBatchKind.Expression : CharacterImageBatchKind.Unmatched;
            string variant = tokens.Length == 3 ? tokens[1] : "", part = tokens.Length == 3 ? tokens[2] : "";
            var row = new CharacterImageBatchRow(input.path.ToString(), input.image?.identity.persistentId ?? Guid.Empty, input.image?.contentVersion ?? 0,
                kind, variant, part, "", "", false, false, "");
            string failure = input.error;
            if (failure.Length == 0 && (kind == CharacterImageBatchKind.Unmatched || string.IsNullOrWhiteSpace(variant) || string.IsNullOrWhiteSpace(part)
                || variant != variant.Trim() || part != part.Trim() || variant.Length > 256 || part.Length > 256))
                failure = "文件名应为 pose__姿势名__部位名 或 expression__表情名__部位名。";
            if (failure.Length == 0 && (input.image is null || input.image.isMissing || input.image.identity.persistentId == Guid.Empty
                || input.image.assetPath != input.path || input.image.width <= 0 || input.image.height <= 0))
                failure = "原生图像未就绪，或导入路径/身份与审核来源不同。";
            if (failure.Length == 0)
            {
                try
                {
                    string key = kind + ":" + variant;
                    if (!groups.TryGetValue(key, out var group)) groups.Add(key, group = ResolveGroup(source, kind, variant, createVariants));
                    string partId = ResolvePart(source, group, part);
                    row = row with { variantId = group.id, partId = partId, createsVariant = group.created, applicable = true,
                        message = group.created ? "可用：创建命名变体并设置图像。" : "可用：替换匹配部位的图像。" };
                }
                catch (InvalidOperationException error) { failure = error.Message; }
            }
            rows.Add((failure.Length > 0 ? row with { applicable = false, message = failure } : row, input.image));
        }
        var duplicates = rows.Where(static item => item.row.applicable).GroupBy(static item =>
            (item.row.kind, item.row.variantId, item.row.partId)).Where(static group => group.Count() > 1).Select(static group => group.Key).ToHashSet();
        for (int index = 0; index < rows.Count; index++)
        {
            var item = rows[index];
            if (item.row.applicable && duplicates.Contains((item.row.kind, item.row.variantId, item.row.partId)))
                rows[index] = (item.row with { applicable = false, message = "冲突：多个图像匹配同一变体/部位，全部保留原值。" }, item.image);
        }
        RejectExcess(CharacterImageBatchKind.Pose, source.poses.Length, 256);
        RejectExcess(CharacterImageBatchKind.Expression, source.expressions.Length, 1024);
        var candidate = CharacterAssetCopy.Copy(source);
        foreach (var item in rows.Where(static item => item.row.applicable && item.row.createsVariant).DistinctBy(static item => (item.row.kind, item.row.variantId)))
        {
            var row = item.row;
            if (row.kind == CharacterImageBatchKind.Pose)
            {
                CharacterPose copy = source.poses.Single(item => item.id == source.defaultPose).Copy(); copy.id = row.variantId; copy.name = row.variant;
                candidate.poses = [.. candidate.poses, copy];
            }
            else
            {
                candidate.expressions = [.. candidate.expressions, new() { id = row.variantId, name = row.variant }];
                if (candidate.expressions.Length == 1) candidate.defaultExpression = row.variantId;
            }
        }
        foreach (var item in rows.Where(static item => item.row.applicable))
        {
            var row = item.row; TextureAsset image = item.image!;
            if (row.kind == CharacterImageBatchKind.Pose) CharacterEditService.SetPartImage(candidate, row.variantId, row.partId, image, resize);
            else
            {
                var expression = candidate.expressions.Single(item => item.id == row.variantId);
                var part = expression.parts.SingleOrDefault(item => item.partId == row.partId) ?? new() { partId = row.partId };
                part.texture = CharacterEditService.ImageIndex(candidate, image);
                expression.parts = [.. expression.parts.Where(item => item.partId != row.partId), part];
            }
        }
        IEnumerable<TextureAsset> images = source.textures.Concat(source.portrait is { } portrait ? new[] { portrait } : [])
            .Concat(rows.Where(static item => item.row.applicable).Select(static item => item.image!));
        TextureAsset[] references = images.ToArray();
        if (references.Any(static image => image.isMissing || image.identity.persistentId == Guid.Empty || image.assetPath.localPath.Length == 0)
            || references.GroupBy(static image => image.identity.persistentId).Any(static group => group.Select(static image => image.contentVersion).Distinct().Count() != 1))
            throw new InvalidOperationException("Character image references are unavailable or changed while preparing the review. Reload the character and images.");
        return new(baseline, rows.Select(static item => item.row), candidate, references, createVariants, resize);

        void RejectExcess(
            CharacterImageBatchKind kind,
            int existing,
            int limit
        ) {
            int count = rows.Where(item => item.row.applicable && item.row.createsVariant && item.row.kind == kind).Select(static item => item.row.variantId).Distinct().Count();
            if (existing + count <= limit) return;
            for (int index = 0; index < rows.Count; index++)
            {
                var item = rows[index];
                if (item.row.applicable && item.row.createsVariant && item.row.kind == kind)
                    rows[index] = (item.row with { applicable = false, message = "创建的变体超过数量预算；请拆分批次或移除未使用变体。" }, item.image);
            }
        }
    }

    /// <summary>Validates every reviewed source generation before replacing any authored value.</summary>
    /// <param name="current">The current detached document candidate owned by native History.</param>
    /// <param name="preview">The exact internally prepared review.</param>
    public static void ApplyReady(
        CharacterAsset current,
        CharacterImageBatchPreview preview
    ) {
        CharacterAsset replacement = preview.Prepare(current);
        CharacterAssetCopy.CopyInto(replacement, current);
    }

    /// <summary>Fingerprints authored properties through the official native serializer without encoding a parallel document format.</summary>
    /// <param name="character">The exact draft to compare with a review.</param>
    /// <returns>A digest of native serialized authored properties.</returns>
    public static string Fingerprint(CharacterAsset character) => Convert.ToHexString(SHA256.HashData(EditorAssets.CaptureProperties(character).data.Span));

    private static Group ResolveGroup(
        CharacterAsset source,
        CharacterImageBatchKind kind,
        string name,
        bool create
    ) {
        var options = kind == CharacterImageBatchKind.Pose ? source.poses.Select(static item => (item.id, item.name)).ToArray()
            : source.expressions.Select(static item => (item.id, item.name)).ToArray();
        var matches = options.Where(item => Same(name, item.id)).Select(static item => item.id).ToArray();
        if (matches.Length == 0) matches = options.Where(item => item.name.Length > 0 && Same(name, item.name)).Select(static item => item.id).ToArray();
        if (matches.Length > 1) throw new AuthoringValidationException($"变体名称不唯一；请在文件名中使用稳定 ID 或先重命名。");
        if (matches.Length == 1) return new(kind, matches[0], false);
        if (!create) throw new AuthoringValidationException($"变体未匹配；先创建变体，或明确允许此批次创建命名变体。");
        return new(kind, Guid.NewGuid().ToString("N"), true);
    }
    private static string ResolvePart(
        CharacterAsset source,
        Group group,
        string name
    ) {
        var parts = (group.kind == CharacterImageBatchKind.Pose
            ? source.poses.Single(item => item.id == (group.created ? source.defaultPose : group.id)).parts.AsEnumerable()
            : source.poses.SelectMany(static pose => pose.parts)).ToArray();
        string[] matches = parts.Where(part => Same(name, part.id)).Select(static part => part.id).Distinct(StringComparer.Ordinal).ToArray();
        if (matches.Length == 0) matches = parts.Where(part => part.name.Length > 0 && Same(name, part.name)).Select(static part => part.id).Distinct(StringComparer.Ordinal).ToArray();
        if (matches.Length != 1) throw new AuthoringValidationException(matches.Length == 0 ? (FormattableString)$"部位未匹配；先在角色中建立共享部位并命名。" : (FormattableString)$"部位名称不唯一；请使用稳定 ID 或先重命名。");
        if (group.kind == CharacterImageBatchKind.Expression && source.poses.Any(pose => !pose.parts.Any(part => part.id == matches[0])))
            throw new AuthoringValidationException($"表情部位并非所有姿势共有；请先修复语义部位归属。");
        return matches[0];
    }
    private static bool Same(
        string left,
        string right
    ) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private sealed record Group(CharacterImageBatchKind kind, string id, bool created);
}
