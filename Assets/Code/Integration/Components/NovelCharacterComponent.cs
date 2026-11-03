using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Scene;
using InnoEngine.Serialization;
using InnoEngine.Reflection;
using InnoEngine.Mathematics;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Integration.Components;

/// <summary>Applies character appearances to explicitly bound native image objects without owning an alternate part tree.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c07")]
public sealed class NovelCharacterComponent : GameComponent
{
    /// <summary>Gets or sets character metadata, pose and expression resources.</summary>
    [SerializableProperty] public CharacterAsset? character { get; set; }
    /// <summary>Gets or sets the last explicitly applied pose identity.</summary>
    [SerializableProperty] public string pose { get; set; } = string.Empty;
    /// <summary>Gets or sets the last explicitly applied expression identity.</summary>
    [SerializableProperty] public string expression { get; set; } = string.Empty;
    /// <summary>Gets or sets whether sprite and breathing clocks advance during native runtime updates.</summary>
    [SerializableProperty] public bool playing { get; set; } = true;
    /// <summary>Gets or sets elapsed pose time; changing it permits passive authoring scrubbing.</summary>
    [SerializableProperty] public double elapsed { get; set; }
    /// <summary>Gets or sets the explicit speaking marker; no audio-phoneme detection is implied.</summary>
    [SerializableProperty] public bool speaking { get; set; }
    /// <summary>Gets or sets elapsed time since the speaking marker began.</summary>
    [SerializableProperty] public double speechElapsed { get; set; }
    /// <summary>Gets or sets breathing amplitude, period and normalized anchor.</summary>
    [SerializableProperty] public CharacterBreathing breathing { get; set; } = new();
    /// <summary>Gets or sets the local breathing anchor in native coordinates.</summary>
    [SerializableProperty] public Vector2 breathingAnchor { get; set; }

    /// <summary>Changes the speaking marker and resets its independent frame clock on entry.</summary>
    /// <param name="value">The new speaking state.</param>
    public void SetSpeaking(bool value) { if (speaking != value) speechElapsed = 0; speaking = value; }

    /// <summary>Applies a complete appearance after validating all resources and native part bindings.</summary>
    /// <param name="poseId">A pose identity, or empty to select the resource default.</param>
    /// <param name="expressionId">An expression identity, or empty to select the resource default.</param>
    /// <param name="change">Optional synchronous native property-edit adapter; authoring callers wrap the complete operation in engine History. Never retained.</param>
    public void ApplyAppearance(
        string poseId,
        string expressionId,
        Action<EngineObject, string, Action>? change = null
    ) {
        CharacterAsset resource = character ?? throw new InvalidOperationException("Assign a character resource before applying an appearance.");
        CharacterAnimationRules.Validate(resource);
        var selectedPose = resource.poses.Single(item => item.id == (poseId.Length == 0 ? resource.defaultPose : poseId));
        string selectedExpression = expressionId.Length == 0 ? resource.defaultExpression : expressionId;
        var face = resource.expressions.SingleOrDefault(item => item.id == selectedExpression);
        if (face is null && (resource.expressions.Length > 0 || expressionId.Length > 0))
            throw new InvalidOperationException("Character expression is missing: " + selectedExpression);
        var parts = Parts().ToDictionary(static item => item.partId, StringComparer.Ordinal);
        if (selectedPose.parts.Any(part => !parts.ContainsKey(part.id)))
            throw new InvalidOperationException("The native character is missing a bound part required by this pose.");
        if (face is not null && face.parts.Any(replace => !selectedPose.parts.Any(part => part.id == replace.partId)))
            throw new InvalidOperationException("The expression targets a part absent from the selected pose.");
        var changes = selectedPose.parts.Select(part =>
        {
            part.surface.Validate(); part.transform.Validate();
            if (!double.IsFinite(part.width) || part.width <= 0 || part.width > 1e9 || !double.IsFinite(part.height) || part.height <= 0 || part.height > 1e9)
                throw new InvalidOperationException("Character part dimensions must be finite and positive: " + part.id);
            var replacement = face?.parts.SingleOrDefault(item => item.partId == part.id);
            if (replacement is not null && (!double.IsFinite(replacement.offsetX) || Math.Abs(replacement.offsetX) > 1e12
                || !double.IsFinite(replacement.offsetY) || Math.Abs(replacement.offsetY) > 1e12 || replacement.visibility is < -1 or > 1))
                throw new InvalidOperationException("Character expression geometry is invalid: " + part.id);
            int imageIndex = replacement?.texture is >= 0 ? replacement.texture : part.texture;
            if (imageIndex < 0 || imageIndex >= resource.textures.Length || part.mask < -1 || part.mask >= resource.textures.Length)
                throw new InvalidOperationException("A character appearance contains a missing texture: " + part.id);
            var target = parts[part.id];
            if (!target.gameObject.TryGetComponent<NovelImageComponent>(out var image) || image is null)
                throw new InvalidOperationException("Character part requires a native image component: " + part.id);
            return (part, replacement, target, image, imageIndex);
        }).ToArray();
        foreach (var item in changes)
        {
            var source = item.part; var image = item.image; var replace = item.replacement;
            Write(image, nameof(image.texture), () => image.texture = resource.textures[item.imageIndex]);
            Write(image, nameof(image.width), () => image.width = (float)source.width);
            Write(image, nameof(image.height), () => image.height = (float)source.height);
            Write(image, nameof(image.visible), () => image.visible = replace?.visibility is >= 0 ? replace.visibility == 1 : source.visible);
            Write(image, nameof(image.pivot), () => image.pivot = new((float)(source.transform.pivotX / source.width), (float)(source.transform.pivotY / source.height)));
            Write(image, nameof(image.uv), () => image.uv = new((float)source.surface.sourceX, (float)source.surface.sourceY, (float)source.surface.sourceWidth, (float)source.surface.sourceHeight));
            Write(image, nameof(image.crop), () => image.crop = new((float)source.surface.cropX, (float)source.surface.cropY, (float)source.surface.cropWidth, (float)source.surface.cropHeight));
            Write(image, nameof(image.color), () => image.color = (source.color & 0xFFFFFF00) | (uint)Math.Round((source.color & 255) * source.transform.opacity));
            Write(image, nameof(image.group), () => image.group = StageLayerGroup.Characters);
            Write(image, nameof(image.order), () => image.order = source.order);
            Write(image, nameof(image.blend), () => image.blend = source.surface.blend);
            Write(image, nameof(image.maskMode), () => image.maskMode = source.surface.maskMode);
            Write(image, nameof(image.mask), () => image.mask = source.mask < 0 ? null : resource.textures[source.mask]);
            Write(item.target, nameof(item.target.animation), () => item.target.animation = source.animation.Copy());
            Write(image.transform, nameof(image.transform.localPosition), () => image.transform.localPosition = new((float)(source.transform.x + (replace?.offsetX ?? 0)), (float)(-source.transform.y - (replace?.offsetY ?? 0)), 0));
            Write(image.transform, nameof(image.transform.localRotation), () => image.transform.localRotation = Quaternion.FromEulerAnglesXYZDegrees(new(0, 0, (float)-source.transform.rotation)));
            Write(image.transform, nameof(image.transform.localScale), () => image.transform.localScale = new((float)source.transform.scaleX, (float)source.transform.scaleY, 1));
        }
        foreach (var unused in parts.Values.Where(part => !selectedPose.parts.Any(source => source.id == part.partId)))
            if (unused.gameObject.TryGetComponent<NovelImageComponent>(out var image) && image is not null) Write(image, nameof(image.visible), () => image.visible = false);
        Write(this, nameof(pose), () => pose = selectedPose.id);
        Write(this, nameof(expression), () => expression = face?.id ?? string.Empty);
        Write(this, nameof(elapsed), () => elapsed = 0);
        Write(this, nameof(speechElapsed), () => speechElapsed = 0);
        void Write(EngineObject owner, string property, Action edit)
        {
            if (change is null) edit(); else change(owner, property, edit);
        }
    }

    /// <summary>Reads actual descendant bindings, including inactive parts, and rejects cross-scene or detached references.</summary>
    /// <returns>The current native part components; nothing is retained as a second hierarchy.</returns>
    public IReadOnlyList<NovelCharacterPartComponent> Parts()
    {
        var result = new List<NovelCharacterPartComponent>();
        foreach (var item in gameObject.scene.GetObjects())
            foreach (var part in item.GetComponents<NovelCharacterPartComponent>())
            {
                if (!ReferenceEquals(part.character, this)) continue;
                Transform? ancestor = part.transform.parent;
                while (ancestor is not null && !ReferenceEquals(ancestor, transform)) ancestor = ancestor.parent;
                if (ancestor is null || string.IsNullOrWhiteSpace(part.partId))
                    throw new InvalidOperationException("A character part must have a semantic ID and belong to its owner's native subtree.");
                result.Add(part);
            }
        if (result.Select(static part => part.partId).Distinct(StringComparer.Ordinal).Count() != result.Count)
            throw new InvalidOperationException("The character has duplicate native part bindings.");
        return result;
    }

    /// <summary>Advances pose and explicit speaking clocks during the owning scene update.</summary>
    /// <param name="seconds">Finite nonnegative elapsed seconds.</param>
    public void Tick(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0 || !double.IsFinite(elapsed) || elapsed < 0 || elapsed + seconds > 1e12
            || !double.IsFinite(speechElapsed) || speechElapsed < 0 || speechElapsed + seconds > 1e12)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        if (!playing || !gameObject.activeInHierarchy) return;
        elapsed += seconds; if (speaking) speechElapsed += seconds;
    }
}
