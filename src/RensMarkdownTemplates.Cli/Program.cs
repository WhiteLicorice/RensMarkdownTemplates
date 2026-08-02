using Microsoft.Extensions.Logging;
using RensMarkdownTemplates;
using RensMarkdownTemplates.Models;
using RensMarkdownTemplates.Services;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    PrintUsage();
    return 0;
}

if (!string.Equals(args[0], "render", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"Unknown command: {args[0]}");
    PrintUsage();
    return 2;
}

var values = ParseArguments(args[1..]);
if (!values.TryGetValue("input", out var inputValue) ||
    !values.TryGetValue("output", out var outputValue))
{
    Console.Error.WriteLine("render requires --input and --output.");
    return 2;
}

var input = Path.GetFullPath(inputValue);
var output = Path.GetFullPath(outputValue);
if (!File.Exists(input))
{
    Console.Error.WriteLine($"Markdown input was not found: {input}");
    return 2;
}

var contentRoot = values.TryGetValue("content-root", out var contentRootValue)
    ? Path.GetFullPath(contentRootValue)
    : FindGitRoot(Path.GetDirectoryName(input)!) ?? Path.GetDirectoryName(input)!;
var pipelineRoot = values.TryGetValue("pipeline-root", out var pipelineRootValue)
    ? Path.GetFullPath(pipelineRootValue)
    : FindPipelineRoot(AppContext.BaseDirectory)
      ?? throw new InvalidOperationException("Cannot locate the RensMarkdownTemplates repository root.");
var cacheRoot = values.TryGetValue("cache-root", out var cacheRootValue)
    ? Path.GetFullPath(cacheRootValue)
    : Path.Combine(contentRoot, ".cache");
var generatedRoot = Path.Combine(cacheRoot, "md-to-pdf", "generated");

var options = new PdfGeneratorOptions
{
    ContentRoot = contentRoot,
    PipelineRoot = pipelineRoot,
    MaterialsDirectory = Path.GetDirectoryName(input)!,
    MaterialFiles = [input],
    IncludeDrafts = true,
    OutputDirectory = generatedRoot,
    ArtifactsDirectory = cacheRoot
};

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddSimpleConsole(console => console.SingleLine = true)
        .SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<PdfGeneratorService>();
var toolchain = new ToolchainProvider(options);
var runner = new SystemProcessRunner();
var cache = new PdfCacheService(toolchain);
var manifest = new PdfGenerationManifest();
var generator = new PdfGeneratorService(
    toolchain, runner, new MermaidRenderer(toolchain, runner), cache, manifest, options);

var route = Path.GetFileNameWithoutExtension(input);
if (values.ContainsKey("force"))
    await cache.InvalidateAsync(route, logger);

await generator.RunAsync(logger);
var result = manifest.GetResult(route);
if (result?.Status is not (PdfGenerationStatus.Generated or PdfGenerationStatus.Cached) ||
    string.IsNullOrWhiteSpace(result.RelativeUrl))
{
    Console.Error.WriteLine(result?.Diagnostic ?? "PDF generation did not return a result.");
    return 1;
}

var generated = Path.Combine(generatedRoot, Path.GetFileName(result.RelativeUrl));
if (!File.Exists(generated))
{
    Console.Error.WriteLine($"Generated PDF was not found: {generated}");
    return 1;
}

Directory.CreateDirectory(Path.GetDirectoryName(output)!);
var temporaryOutput = output + $".{Guid.NewGuid():N}.tmp";
File.Copy(generated, temporaryOutput, overwrite: true);
File.Move(temporaryOutput, output, overwrite: true);
Console.WriteLine(output);
return 0;

static Dictionary<string, string> ParseArguments(string[] arguments)
{
    var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < arguments.Length; index++)
    {
        var argument = arguments[index];
        if (!argument.StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"Unexpected argument: {argument}");

        var key = argument[2..];
        if (key == "force")
        {
            parsed[key] = "true";
            continue;
        }

        if (++index >= arguments.Length)
            throw new ArgumentException($"Missing value for --{key}.");
        parsed[key] = arguments[index];
    }
    return parsed;
}

static string? FindGitRoot(string start)
{
    for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        if (Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
            File.Exists(Path.Combine(directory.FullName, ".git")))
            return directory.FullName;
    return null;
}

static string? FindPipelineRoot(string start)
{
    for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        if (File.Exists(Path.Combine(directory.FullName, "templates", "default", "template.latex")))
            return directory.FullName;
    return null;
}

static void PrintUsage()
{
    Console.WriteLine("RensMarkdownTemplates");
    Console.WriteLine("  render --input FILE --output FILE [--content-root DIR] [--cache-root DIR] [--pipeline-root DIR] [--force]");
}
