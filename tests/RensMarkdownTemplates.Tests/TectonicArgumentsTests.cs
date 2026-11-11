using System.Text;
using Microsoft.Extensions.Logging;

namespace RensMarkdownTemplates.Tests;

public class TectonicArgumentsTests
{
    private const string BundleUrl = "https://data1.fullyjustified.net/tlextras-2022.0r0.tar";
    private const string OnlyCachedVariable = "PDF_TECTONIC_ONLY_CACHED";
    private const string CacheDirectoryVariable = "PDF_TECTONIC_CACHE_DIR";

    [Fact]
    public void DefaultArguments_OmitOnlyCached()
    {
        var args = PdfGeneratorService.BuildTectonicArguments(
            "document.tex", "work", BundleUrl, onlyCached: false);

        Assert.DoesNotContain("--only-cached", args);
        Assert.Equal(["-X", "compile"], args[..2]);
    }

    [Fact]
    public void OnlyCachedArguments_PlaceTheFlagAfterCompile()
    {
        var args = PdfGeneratorService.BuildTectonicArguments(
            "document.tex", "work", BundleUrl, onlyCached: true);

        var flag = Array.IndexOf(args, "--only-cached");
        Assert.True(flag > 0, "--only-cached should be present");
        Assert.Equal("compile", args[flag - 1]);
        Assert.True(flag < Array.IndexOf(args, "--untrusted"),
            "--only-cached should precede the remaining options");
    }

    [Fact]
    public void Arguments_KeepTheBundleAndOutputShape()
    {
        var args = PdfGeneratorService.BuildTectonicArguments(
            "document.tex", "work", BundleUrl, onlyCached: true);

        Assert.Equal(BundleUrl, args[Array.IndexOf(args, "--bundle") + 1]);
        Assert.Equal("work", args[Array.IndexOf(args, "--outdir") + 1]);
        Assert.Equal("document.tex", args[^1]);
    }

    [Fact]
    public async Task Service_Defaults_OmitOnlyCachedAndUseTheComputedCache()
    {
        var result = await RenderAsync(root => Options(root));

        Assert.DoesNotContain("--only-cached", result.Args);
        Assert.Equal(Path.Combine(result.Root, "artifacts", "tectonic-cache"),
            result.CacheDirectory);
    }

    [Fact]
    public async Task Service_Option_EnablesOnlyCached()
    {
        var result = await RenderAsync(root => new PdfGeneratorOptions
        {
            ContentRoot = root,
            PipelineRoot = root,
            OnlyCachedTectonic = true
        });

        Assert.Contains("--only-cached", result.Args);
    }

    [Fact]
    public async Task Service_Environment_EnablesOnlyCached()
    {
        var result = await RenderAsync(
            root => Options(root),
            root => new() { [OnlyCachedVariable] = "1" });

        Assert.Contains("--only-cached", result.Args);
    }

    [Fact]
    public async Task Service_EnabledEnvironment_OverridesExplicitFalse()
    {
        var result = await RenderAsync(
            root => new PdfGeneratorOptions
            {
                ContentRoot = root,
                PipelineRoot = root,
                OnlyCachedTectonic = false
            },
            root => new() { [OnlyCachedVariable] = "true" });

        Assert.Contains("--only-cached", result.Args);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("yes", false)]
    [InlineData(" true ", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void BooleanParsing_EnablesOnlyOneOrDefaultTrue(string? value, bool expected)
    {
        Assert.Equal(expected, PdfGeneratorService.IsEnabled(value));
    }

    [Fact]
    public async Task Service_EnvironmentCacheDirectory_IsUsedWhenTheOptionIsAbsent()
    {
        var result = await RenderAsync(
            root => Options(root),
            root => new() { [CacheDirectoryVariable] = Path.Combine(root, "shared-cache") });

        Assert.Equal(Path.GetFullPath(Path.Combine(result.Root, "shared-cache")),
            result.CacheDirectory);
    }

    [Fact]
    public async Task Service_OptionCacheDirectory_WinsOverTheEnvironment()
    {
        var result = await RenderAsync(
            root => new PdfGeneratorOptions
            {
                ContentRoot = root,
                PipelineRoot = root,
                TectonicCacheDirectory = Path.Combine(root, "option-cache")
            },
            root => new() { [CacheDirectoryVariable] = Path.Combine(root, "env-cache") });

        Assert.Equal(Path.GetFullPath(Path.Combine(result.Root, "option-cache")),
            result.CacheDirectory);
    }

    [Fact]
    public async Task Service_RelativeCacheDirectory_ResolvesAgainstTheCurrentDirectory()
    {
        var result = await RenderAsync(root => new PdfGeneratorOptions
        {
            ContentRoot = root,
            PipelineRoot = root,
            TectonicCacheDirectory = Path.GetRelativePath(
                Environment.CurrentDirectory, Path.Combine(root, "relative-cache"))
        });

        Assert.Equal(Path.GetFullPath(Path.Combine(result.Root, "relative-cache")),
            result.CacheDirectory);
    }

    [Fact]
    public async Task IncompleteCache_FailureDiagnosticNamesTheMissingFont()
    {
        var stderr = new StringBuilder();
        for (var i = 0; i < 120; i++)
            stderr.AppendLine($"warning: document.tex:{i}: Underfull \\hbox (badness {i}) in paragraph at lines {i}--{i + 1}");
        stderr.AppendLine("warning: Tectonic unable to generate PK font \"cmmi6.pfb\" (dpi 600) on-the-fly");
        stderr.AppendLine("warning: Could not locate a virtual/physical font for TFM \"cmmi6\".");
        stderr.AppendLine("error: Cannot proceed without .vf or \"physical\" font for PDF output...");
        Assert.True(stderr.Length > 2000, "the fixture must exceed the truncated stderr window");

        var result = await RenderAsync(
            root => Options(root),
            tectonicStdErr: stderr.ToString());

        var manifest = result.Manifest.GetResult("lesson");
        Assert.Equal(PdfGenerationStatus.Failed, manifest?.Status);
        Assert.Contains(result.Logger.Warnings, warning => warning.Contains("cmmi6.pfb"));
        Assert.Contains("cmmi6.pfb", manifest?.Diagnostic);
    }

    private static PdfGeneratorOptions Options(string root) => new()
    {
        ContentRoot = root,
        PipelineRoot = root
    };

    private sealed record RenderResult(string Root, string[] Args, string? CacheDirectory,
        PdfGenerationManifest Manifest, CollectingLogger Logger);

    private static async Task<RenderResult> RenderAsync(
        Func<string, PdfGeneratorOptions> optionsFor,
        Func<string, Dictionary<string, string?>>? environmentFor = null,
        string? tectonicStdErr = null)
    {
        using var tempDir = new TempDirectory();
        await WritePipelineAsync(tempDir.Path);

        var toolchain = new RecordingToolchainProvider(tempDir.Path);
        var runner = new CapturingProcessRunner { TectonicStdErr = tectonicStdErr };
        var manifest = new PdfGenerationManifest();
        var logger = new CollectingLogger();

        var names = new[] { OnlyCachedVariable, CacheDirectoryVariable };
        var saved = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var name in names)
                Environment.SetEnvironmentVariable(name, null);
            foreach (var (name, value) in environmentFor?.Invoke(tempDir.Path) ?? new())
                Environment.SetEnvironmentVariable(name, value);

            var generator = new PdfGeneratorService(toolchain, runner, new NoOpMermaidRenderer(),
                new PdfCacheService(toolchain), manifest, optionsFor(tempDir.Path));
            await generator.RunAsync(logger);
        }
        finally
        {
            foreach (var (name, value) in saved)
                Environment.SetEnvironmentVariable(name, value);
        }

        var call = Assert.Single(runner.Calls, entry => entry.Executable == "fake-tectonic");
        return new RenderResult(tempDir.Path, call.Args,
            call.Environment?["TECTONIC_CACHE_DIR"], manifest, logger);
    }

    private static async Task WritePipelineAsync(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "Content", "Materials"));
        Directory.CreateDirectory(Path.Combine(root, "PdfTemplates", "default"));
        await File.WriteAllTextAsync(
            Path.Combine(root, "PdfTemplates", "default", "template.latex"), "$body$");
        await File.WriteAllTextAsync(Path.Combine(root, ".mmdc.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(root, "package-lock.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(root, "Content", "Materials", "lesson.md"),
            "---\ntitle: Lesson\npublished: 2026-03-01\n---\n\nBody\n");
    }

    private sealed record ProcessCall(string Executable, string[] Args,
        Dictionary<string, string?>? Environment);

    private sealed class CapturingProcessRunner : IProcessRunner
    {
        public List<ProcessCall> Calls { get; } = new();
        public string? TectonicStdErr { get; init; }

        public async Task<ProcessResult> RunAsync(string executable, string[] args,
            string? workingDirectory = null,
            Dictionary<string, string?>? environmentVariables = null,
            int timeoutMs = 180_000,
            CancellationToken ct = default)
        {
            Calls.Add(new ProcessCall(executable, args, environmentVariables is null
                ? null
                : new Dictionary<string, string?>(environmentVariables)));

            if (executable == "fake-pandoc")
            {
                var output = args[Array.IndexOf(args, "--output") + 1];
                await File.WriteAllTextAsync(output, "\\begin{document}ok\\end{document}", ct);
            }
            else if (executable == "fake-tectonic")
            {
                if (TectonicStdErr is not null)
                    return new ProcessResult { ExitCode = 1, StdErr = TectonicStdErr };

                var outDir = args[Array.IndexOf(args, "--outdir") + 1];
                await File.WriteAllTextAsync(Path.Combine(outDir, "document.pdf"), "%PDF-1.7\n", ct);
            }

            return new ProcessResult { ExitCode = 0 };
        }
    }
}
