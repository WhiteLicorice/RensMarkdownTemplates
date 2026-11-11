namespace RensMarkdownTemplates.Tests;

public class TectonicArgumentsTests
{
    private const string BundleUrl = "https://data1.fullyjustified.net/tlextras-2022.0r0.tar";

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
}
