using System.Linq;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Authoring.Templates;

/// <summary>Resolves explicitly packaged original teaching scenes and layered actors.</summary>
public static class BuiltInVisualLibrary
{
    /// <summary>Loads six native scenes without scanning the user's project or installation directories.</summary>
    /// <returns>Canonical read-only built-in definitions; copy values before editing.</returns>
    public static StageAsset[] Stages() => new[] { "Classroom", "Station", "Seafront", "Woodland", "Bedroom", "Rooftop" }
        .Select(static name => BuiltInNovelAssets.Load<StageAsset>("BuiltIn/Backgrounds/" + name + ".ivnstage")).ToArray();
    /// <summary>Loads two original actors, each with two poses, six expressions and independent image layers.</summary>
    /// <returns>Canonical read-only definitions; native image references remain explicit.</returns>
    public static CharacterAsset[] Characters() => new[] { "Cheng", "Shu" }
        .Select(static name => BuiltInNovelAssets.Load<CharacterAsset>("BuiltIn/Characters/" + name + "/" + name + ".ivncharacter")).ToArray();
}
