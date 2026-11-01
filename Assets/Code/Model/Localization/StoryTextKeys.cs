using System.Globalization;

namespace Inno.VisualNovel.Model.Localization;

/// <summary>Builds collision-free text addresses shared by extraction and runtime resolution.</summary>
public static class StoryTextKeys
{
    /// <summary>Addresses a stable dialogue body.</summary>
    /// <param name="textId">The authored line identity.</param>
    /// <returns>The localization table key.</returns>
    public static string Dialogue(string textId) => "line/" + Part(textId) + "/text";
    /// <summary>Addresses a stable dialogue speaker.</summary>
    /// <param name="textId">The authored line identity.</param>
    /// <returns>The localization table key.</returns>
    public static string Speaker(string textId) => "line/" + Part(textId) + "/speaker";
    /// <summary>Addresses a character's name independently of the number of spoken lines.</summary>
    /// <param name="characterId">The stable character resource identity.</param>
    /// <returns>The localization table key.</returns>
    public static string CharacterName(string characterId) => "character/" + Part(characterId) + "/name";
    /// <summary>Addresses a node-owned field without depending on linked instruction indices.</summary>
    /// <param name="chapter">The authored chapter identity.</param>
    /// <param name="node">The authored node identity.</param>
    /// <param name="field">The stable field or option identity.</param>
    /// <returns>The localization table key.</returns>
    public static string Node(
        string chapter,
        string node,
        string field
    ) => "node/" + Part(chapter) + "/" + Part(node) + "/" + Part(field);
    /// <summary>Addresses a system-page element independently of its layout.</summary>
    /// <param name="role">The stable page role.</param>
    /// <param name="element">The stable element identity.</param>
    /// <returns>The localization table key.</returns>
    public static string Ui(
        string role,
        string element
    ) => "ui/" + Part(role) + "/" + Part(element);
    /// <summary>Addresses a collection item's translatable metadata.</summary>
    /// <param name="id">The stable collection identity.</param>
    /// <param name="field">The metadata field identity.</param>
    /// <returns>The localization table key.</returns>
    public static string Collection(
        string id,
        string field
    ) => "collection/" + Part(id) + "/" + Part(field);
    private static string Part(string value) => value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;
}
