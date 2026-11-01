using System.Collections.Generic;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class OriginalParticleLibrary
{
    internal static IReadOnlyList<VisualEffectAsset> Create() => [
        new() { resourceId = "builtin.particles.rain", displayName = "细雨", particles = new() { rate = 220, width = 1920, height = 1080, minimumLifetime = 1.2, maximumLifetime = 1.8,
            minimumVelocityX = -180, maximumVelocityX = -120, minimumVelocityY = 760, maximumVelocityY = 900, startSize = 32, endSize = 32, aspect = 0.15,
            minimumRotation = 10, maximumRotation = 10, angularSpeed = 0, startColor = 0xBED8F090, endColor = 0xBED8F000 } },
        new() { resourceId = "builtin.particles.snow", displayName = "落雪", particles = new() { rate = 45, width = 1920, height = 1080, minimumLifetime = 6, maximumLifetime = 10,
            minimumVelocityX = -30, maximumVelocityX = 20, minimumVelocityY = 45, maximumVelocityY = 85, startSize = 14, endSize = 8,
            angularSpeed = 24, startColor = 0xE7F2FAD0, endColor = 0xE7F2FA00 } },
        new() { resourceId = "builtin.particles.dust", displayName = "浮尘", particles = new() { rate = 20, width = 1920, height = 1080, minimumLifetime = 6, maximumLifetime = 12,
            minimumVelocityX = -8, maximumVelocityX = 8, minimumVelocityY = -15, maximumVelocityY = -4, startSize = 8, endSize = 3,
            angularSpeed = 0, startColor = 0xF2E4BB90, endColor = 0xF2E4BB00 } },
        new() { resourceId = "builtin.particles.petals", displayName = "花瓣", particles = new() { rate = 18, width = 1920, height = 300, minimumLifetime = 5, maximumLifetime = 9,
            minimumVelocityX = 30, maximumVelocityX = 80, minimumVelocityY = 70, maximumVelocityY = 120, startSize = 28, endSize = 22,
            angularSpeed = 70, startColor = 0xF3B5C8E0, endColor = 0xF3B5C800 } },
        new() { resourceId = "builtin.particles.fireflies", displayName = "萤火", particles = new() { rate = 12, width = 1920, height = 1080, minimumLifetime = 4, maximumLifetime = 7,
            minimumVelocityX = -20, maximumVelocityX = 20, minimumVelocityY = -35, maximumVelocityY = -10, startSize = 26, endSize = 12,
            angularSpeed = 0, startColor = 0xD6ED8CE0, endColor = 0xD6ED8C00,
            sizeKeys = [new() { time = 0, value = 8, interpolation = PerformanceInterpolation.Smooth }, new() { time = .35, value = 26, interpolation = PerformanceInterpolation.Smooth }, new() { time = 1, value = 4 }],
            colorKeys = [new() { progress = 0, color = 0xD6ED8C00, interpolation = PerformanceInterpolation.Smooth }, new() { progress = .2, color = 0xD6ED8CE0 },
                new() { progress = .65, color = 0xD6ED8CE0, interpolation = PerformanceInterpolation.Smooth }, new() { progress = 1, color = 0xD6ED8C00 }] } },
        new() { resourceId = "builtin.particles.lightpoints", displayName = "柔光点", particles = new() { rate = 16, width = 1920, height = 1080, minimumLifetime = 3, maximumLifetime = 6,
            minimumVelocityX = -10, maximumVelocityX = 10, minimumVelocityY = -25, maximumVelocityY = -5, startSize = 38, endSize = 8,
            angularSpeed = 0, startColor = 0xD3E9FFC0, endColor = 0xD3E9FF00 } },
        new() { resourceId = "builtin.particles.bubbles", displayName = "气泡", particles = new() { rate = 15, width = 1920, height = 1080, minimumLifetime = 4, maximumLifetime = 7,
            minimumVelocityX = -12, maximumVelocityX = 12, minimumVelocityY = -100, maximumVelocityY = -50, startSize = 16, endSize = 44,
            angularSpeed = 8, startColor = 0xC6E7F2B0, endColor = 0xC6E7F200 } },
        new() { resourceId = "builtin.particles.confetti", displayName = "纸屑", particles = new() { rate = 180, duration = 0.7, width = 1920, height = 100, minimumLifetime = 2.5, maximumLifetime = 4.5,
            minimumVelocityX = -180, maximumVelocityX = 180, minimumVelocityY = 40, maximumVelocityY = 180, accelerationY = 110, startSize = 24, endSize = 18,
            angularSpeed = 180, startColor = 0xF4CE8FFF, endColor = 0xF4CE8F00 } }
    ];
}
