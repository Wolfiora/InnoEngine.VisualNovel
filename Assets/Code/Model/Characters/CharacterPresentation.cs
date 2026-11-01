using System.Collections.Generic;
using InnoEngine.Rendering;

namespace Inno.VisualNovel.Model.Characters;

/// <summary>Exposes frozen speaker metadata without exposing editable pose declarations.</summary>
/// <param name="id">The stable character identity.</param>
/// <param name="displayName">The resolved game-language name.</param>
/// <param name="aliases">Read-only alternative names.</param>
/// <param name="nameColor">The packed RGBA dialogue name tint.</param>
/// <param name="portrait">The optional borrowed imported portrait.</param>
public sealed record CharacterPresentation(string id, string displayName, IReadOnlyList<string> aliases, uint nameColor, TextureAsset? portrait);
