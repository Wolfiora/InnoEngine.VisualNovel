using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

namespace Inno.VisualNovel.Tools.ArchitectureCheck;

internal static partial class Program
{
    private static int Main(string[] args)
    {
        if (args.Length is not (2 or 3))
            throw new ArgumentException("Supply the project root and formal compiled script output.");
        string project = Path.GetFullPath(args[0]);
        string output = Path.GetFullPath(args[1]);
        int errors = 0;
        int assemblies = 0;
        foreach (string path in Directory.EnumerateFiles(output, "Inno.VisualNovel.*.dll"))
        {
            assemblies++;
            string name = Path.GetFileNameWithoutExtension(path);
            bool editor = name is "Inno.VisualNovel.Compiler" or "Inno.VisualNovel.Authoring" or "Inno.VisualNovel.Editor";
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            MetadataReader metadata = pe.GetMetadataReader();
            foreach (AssemblyReferenceHandle handle in metadata.AssemblyReferences)
            {
                string reference = metadata.GetString(metadata.GetAssemblyReference(handle).Name);
                if (reference.Contains("Rendering2D", StringComparison.OrdinalIgnoreCase)
                    || reference.Contains("Canvas", StringComparison.OrdinalIgnoreCase)
                    || reference.StartsWith("Inno.Native.", StringComparison.Ordinal)
                    || !editor && (reference.StartsWith("Inno.Editor.", StringComparison.Ordinal)
                        || reference is "Inno.VisualNovel.Compiler" or "Inno.VisualNovel.Authoring" or "Inno.VisualNovel.Editor"))
                    Fail($"{name} has forbidden dependency {reference}.");
            }
        }
        foreach (string path in Directory.EnumerateFiles(Path.Combine(project, "Assets", "Code"), "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(path);
            if (ForbiddenImport().IsMatch(source))
                Fail($"{Path.GetRelativePath(project, path)} imports an implementation namespace or forbidden plugin.");
            if (source.Contains("NotImplementedException", StringComparison.Ordinal) || source.Contains("InternalsVisibleTo", StringComparison.Ordinal))
                Fail($"{Path.GetRelativePath(project, path)} contains a placeholder or test backdoor.");
            string? directory = Path.GetDirectoryName(path);
            while (directory is not null && !Directory.EnumerateFiles(directory, "*.iasmdef").Any())
            {
                if (directory == Path.Combine(project, "Assets", "Code"))
                {
                    Fail($"{path} has no owning assembly definition.");
                    break;
                }
                directory = Path.GetDirectoryName(directory);
            }
        }
        if (assemblies == 0) Fail("No formal plugin assemblies were found.");
        Console.WriteLine($"Architecture: {assemblies} assemblies checked, {errors} errors.");
        if (args.Length == 3) ApiSurfaceReport.Write(args[2]);
        return errors == 0 ? 0 : 1;

        void Fail(string message) { errors++; Console.Error.WriteLine(message); }
    }

    [GeneratedRegex(@"(?m)^\s*(?:global\s+)?using\s+(?:Inno\.(?!VisualNovel\b)|InnoEngine\.(?:Canvas|Rendering2D)\b)")]
    private static partial Regex ForbiddenImport();
}
