using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Inno.VisualNovel.Model.Extensions;

/// <summary>Hashes explicitly supplied VN implementation metadata for exact-build saves without discovering types or retaining CLR metadata.</summary>
public static class NovelImplementationFingerprint
{
    /// <summary>Identifies the published code module and exact type selected by an explicit registration.</summary>
    /// <param name="implementation">A known implementation type, never a type discovered by scanning assemblies.</param>
    /// <returns>An opaque fingerprint; no CLR type or module is persisted.</returns>
    public static string TypeCode(Type implementation)
    {
        ArgumentNullException.ThrowIfNull(implementation);
        return Combine([implementation.Module.ModuleVersionId.ToString("N"), implementation.FullName ?? throw new ArgumentException("Implementation types must have a published name.", nameof(implementation))]);
    }
    /// <summary>Identifies one explicitly registered factory's published code and method binding.</summary>
    /// <param name="factory">One generation-owned single factory; captured mutable state is not a substitute for authored parameters.</param>
    /// <returns>A fingerprint of public metadata only, without invoking or retaining the factory.</returns>
    public static string FactoryCode(Delegate factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (factory.GetInvocationList().Length != 1) throw new ArgumentException("A VN factory must have one invocation target.", nameof(factory));
        return Combine([factory.Method.Module.ModuleVersionId.ToString("N"), factory.Method.MetadataToken.ToString(System.Globalization.CultureInfo.InvariantCulture),
            factory.Method.DeclaringType?.FullName ?? string.Empty, factory.Method.Name]);
    }
    /// <summary>Combines named implementation or content contributions independently of registration order.</summary>
    /// <param name="contributions">Explicit code/content contributions; values are hashed with length boundaries.</param>
    /// <returns>A deterministic SHA-256 fingerprint for this exact contribution set.</returns>
    public static string Combine(IEnumerable<string> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[4];
        foreach (string value in contributions.Order(StringComparer.Ordinal))
        {
            ArgumentNullException.ThrowIfNull(value);
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
            hash.AppendData(length); hash.AppendData(bytes);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
