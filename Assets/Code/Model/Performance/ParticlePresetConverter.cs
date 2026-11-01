using InnoEngine.Rendering;
using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Model.Performance;

/// <summary>Preserves optional sprite references and closed particle parameters through native serialization.</summary>
public sealed class ParticlePresetConverter : SerializationConverter<ParticlePreset>
{
    /// <inheritdoc />
    public override void Write(
        SerializationWriter writer,
        ParticlePreset value
    ) {
        writer.Write<TextureAsset?>("texture", value.texture); writer.Write("group", value.group); writer.Write("order", value.order); writer.Write("parallax", value.parallax);
        writer.Write("width", value.width); writer.Write("height", value.height); writer.Write("rate", value.rate); writer.Write("duration", value.duration);
        writer.Write("minimumLifetime", value.minimumLifetime); writer.Write("maximumLifetime", value.maximumLifetime);
        writer.Write("minimumVelocityX", value.minimumVelocityX); writer.Write("maximumVelocityX", value.maximumVelocityX);
        writer.Write("minimumVelocityY", value.minimumVelocityY); writer.Write("maximumVelocityY", value.maximumVelocityY);
        writer.Write("accelerationX", value.accelerationX); writer.Write("accelerationY", value.accelerationY);
        writer.Write("startSize", value.startSize); writer.Write("endSize", value.endSize); writer.Write("aspect", value.aspect);
        writer.Write("minimumRotation", value.minimumRotation); writer.Write("maximumRotation", value.maximumRotation); writer.Write("angularSpeed", value.angularSpeed);
        writer.Write("startColor", value.startColor); writer.Write("endColor", value.endColor); writer.Write("essential", value.essential);
        writer.Write("sizeKeys", value.sizeKeys); writer.Write("colorKeys", value.colorKeys);
    }
    /// <inheritdoc />
    public override ParticlePreset Read(SerializationReader reader) => new()
    {
        texture = reader.Read<TextureAsset?>("texture"), group = reader.Read<StageLayerGroup>("group"), order = reader.Read<int>("order"), parallax = reader.Read<double>("parallax"),
        width = reader.Read<double>("width"), height = reader.Read<double>("height"), rate = reader.Read<double>("rate"), duration = reader.Read<double>("duration"),
        minimumLifetime = reader.Read<double>("minimumLifetime"), maximumLifetime = reader.Read<double>("maximumLifetime"),
        minimumVelocityX = reader.Read<double>("minimumVelocityX"), maximumVelocityX = reader.Read<double>("maximumVelocityX"),
        minimumVelocityY = reader.Read<double>("minimumVelocityY"), maximumVelocityY = reader.Read<double>("maximumVelocityY"),
        accelerationX = reader.Read<double>("accelerationX"), accelerationY = reader.Read<double>("accelerationY"),
        startSize = reader.Read<double>("startSize"), endSize = reader.Read<double>("endSize"), aspect = reader.Read<double>("aspect"),
        minimumRotation = reader.Read<double>("minimumRotation"), maximumRotation = reader.Read<double>("maximumRotation"), angularSpeed = reader.Read<double>("angularSpeed"),
        startColor = reader.Read<uint>("startColor"), endColor = reader.Read<uint>("endColor"), essential = reader.Read<bool>("essential"),
        sizeKeys = reader.Read<PerformanceKeyframe[]>("sizeKeys"), colorKeys = reader.Read<ParticleColorKeyframe[]>("colorKeys")
    };
}
