namespace RensMarkdownTemplates;

public sealed class PdfGeneratorOptions
{
    public required string ContentRoot { get; init; }
    public required string PipelineRoot { get; init; }
    public string MaterialsDirectory { get; init; } = Path.Combine("Content", "Materials");
    public IReadOnlyList<string>? MaterialFiles { get; init; }
    public bool IncludeDrafts { get; init; }
    public string OutputDirectory { get; init; } = Path.Combine("wwwroot", "pdfs");
    public string ArtifactsDirectory { get; init; } = "artifacts";

    public string ResolveMaterialsDirectory() => ResolveFromContentRoot(MaterialsDirectory);
    public string ResolveOutputDirectory() => ResolveFromContentRoot(OutputDirectory);
    public string ResolveArtifactsDirectory() => ResolveFromContentRoot(ArtifactsDirectory);

    private string ResolveFromContentRoot(string path) =>
        Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(ContentRoot, path));
}
