using System;
using System.Linq;
using InnoEngine.Rendering;
using Inno.VisualNovel.Model.Characters;

namespace Inno.VisualNovel.Authoring.CharacterEditing;

/// <summary>Owns nondestructive pose, part and expression edits for character resource drafts.</summary>
public static class CharacterEditService
{
    /// <summary>Creates a usable single-part character from a canonical imported image.</summary>
    /// <param name="texture">The imported character image.</param>
    /// <returns>A detached character with a valid default pose and neutral expression.</returns>
    public static CharacterAsset FromImage(TextureAsset texture)
    {
        Require(texture);
        return new() { resourceId = "character-" + Guid.NewGuid().ToString("N"), textures = [texture], poses = [new() { parts = [
            new() { id = "body", name = texture.name, texture = 0, width = texture.width, height = texture.height }] }], expressions = [new()] };
    }
    /// <summary>Duplicates a pose with independent part values while preserving semantic part identities.</summary>
    /// <param name="character">The candidate character.</param>
    /// <param name="poseId">The pose to copy.</param>
    /// <returns>The new pose identity.</returns>
    public static string DuplicatePose(
        CharacterAsset character,
        string poseId
    ) {
        if (character.poses.Length >= 256) throw new InvalidOperationException("A character supports at most 256 poses.");
        CharacterPose original = character.poses.Single(item => item.id == poseId);
        string id = Guid.NewGuid().ToString("N");
        CharacterPose copy = original.Copy(); copy.id = id; copy.name += " Copy";
        character.poses = [.. character.poses, copy];
        return id;
    }
    /// <summary>Replaces the base image of one pose part without changing other poses, masks or animation frames.</summary>
    /// <param name="character">The detached character candidate.</param>
    /// <param name="poseId">The exact owning pose.</param>
    /// <param name="partId">The stable semantic part.</param>
    /// <param name="texture">The canonical imported replacement.</param>
    /// <param name="resize">Whether authored geometry should adopt the replacement's pixel dimensions.</param>
    public static void SetPartImage(
        CharacterAsset character,
        string poseId,
        string partId,
        TextureAsset texture,
        bool resize = false
    ) {
        var part = character.poses.Single(item => item.id == poseId).parts.Single(item => item.id == partId);
        int index = ImageIndex(character, texture); part.texture = index;
        if (resize) { part.width = texture.width; part.height = texture.height; }
    }
    /// <summary>Adds a part to every pose so shared expression overrides remain valid.</summary>
    /// <param name="character">The candidate character.</param>
    /// <param name="texture">The canonical part image.</param>
    /// <returns>The shared semantic part identity.</returns>
    public static string AddPart(
        CharacterAsset character,
        TextureAsset texture
    ) {
        int image = ImageIndex(character, texture);
        string id = Guid.NewGuid().ToString("N");
        foreach (CharacterPose pose in character.poses) pose.parts = [.. pose.parts, new() { id = id, name = texture.name, texture = image, width = texture.width, height = texture.height, order = pose.parts.Length }];
        return id;
    }
    /// <summary>Removes a semantic part from every pose and its explicit expression overrides.</summary>
    /// <param name="character">The candidate character.</param>
    /// <param name="partId">The shared part identity.</param>
    public static void RemovePart(
        CharacterAsset character,
        string partId
    ) {
        foreach (CharacterPose pose in character.poses) pose.parts = pose.parts.Where(item => item.id != partId).ToArray();
        foreach (CharacterExpression expression in character.expressions) expression.parts = expression.parts.Where(item => item.partId != partId).ToArray();
    }
    /// <summary>Returns a stable index for an imported image, appending only when not already referenced.</summary>
    /// <param name="character">The candidate character.</param>
    /// <param name="texture">The canonical imported image.</param>
    /// <returns>The character-local image index.</returns>
    public static int ImageIndex(
        CharacterAsset character,
        TextureAsset texture
    ) {
        Require(texture);
        int index = Array.FindIndex(character.textures, item => item.identity.persistentId == texture.identity.persistentId);
        if (index >= 0) return index;
        index = character.textures.Length; character.textures = [.. character.textures, texture]; return index;
    }
    private static void Require(TextureAsset texture)
    {
        if (texture.isMissing || texture.identity.persistentId == Guid.Empty) throw new InvalidOperationException("Select an imported character image first.");
    }
}
