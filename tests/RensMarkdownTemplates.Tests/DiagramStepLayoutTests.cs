namespace RensMarkdownTemplates.Tests;

/// <summary>
/// A learning-diagram step is a heading, its description, and one figure. The
/// three have to reach the same page, so the generated Markdown wraps them in
/// the template's keep-together unit.
/// </summary>
public class DiagramStepLayoutTests
{
    private const string Opener = "\\begin{keepstep}";
    private const string Closer = "\\end{keepstep}";

    private static LearningDiagramStep Step() => new()
    {
        Title = "The trap enters the kernel",
        Description = "The processor switches to kernel mode before it runs the handler.",
        Mermaid = "flowchart LR\n    A --> B"
    };

    [Fact]
    public void Step_OpensKeepTogetherUnitBeforeHeading()
    {
        var markdown = PdfGeneratorService.BuildStepMarkdown(Step(), "mmd_0001.pdf");

        var opener = markdown.IndexOf(Opener, StringComparison.Ordinal);
        var heading = markdown.IndexOf("### The trap enters the kernel", StringComparison.Ordinal);

        Assert.True(opener >= 0, "the step does not open a keep-together unit");
        Assert.True(heading > opener, "the keep-together unit opens after the step heading");
    }

    [Fact]
    public void Step_ClosesKeepTogetherUnitAfterFigure()
    {
        var markdown = PdfGeneratorService.BuildStepMarkdown(Step(), "mmd_0001.pdf");

        var figure = markdown.IndexOf("![The trap enters the kernel](mmd_0001.pdf)", StringComparison.Ordinal);
        var closer = markdown.IndexOf(Closer, StringComparison.Ordinal);

        Assert.True(figure >= 0, "the step does not carry its figure");
        Assert.True(closer > figure, "the keep-together unit closes before the step figure");
    }

    [Fact]
    public void Step_KeepsHeadingDescriptionAndFigureInsideOneUnit()
    {
        var markdown = PdfGeneratorService.BuildStepMarkdown(Step(), "mmd_0001.pdf");

        var opener = markdown.IndexOf(Opener, StringComparison.Ordinal);
        var closer = markdown.IndexOf(Closer, StringComparison.Ordinal);
        Assert.True(opener >= 0 && closer > opener, "the step is not one keep-together unit");

        var inside = markdown[opener..closer];
        Assert.Contains("### The trap enters the kernel", inside);
        Assert.Contains("The processor switches to kernel mode before it runs the handler.", inside);
        Assert.Contains("![The trap enters the kernel](mmd_0001.pdf)", inside);
    }

    [Fact]
    public void Step_LeavesFigureReferenceAndScaleAlone()
    {
        var markdown = PdfGeneratorService.BuildStepMarkdown(Step(), "mmd_0007.pdf");

        // The keep-together unit must not resize the figure, so the image line
        // stays exactly what the generator wrote before the unit existed.
        Assert.Contains("![The trap enters the kernel](mmd_0007.pdf)", markdown);
        Assert.DoesNotContain("width=", markdown);
        Assert.DoesNotContain("height=", markdown);
    }

    [Fact]
    public void Step_WithoutDescription_StillWrapsHeadingAndFigure()
    {
        var step = Step();
        step.Description = "";

        var markdown = PdfGeneratorService.BuildStepMarkdown(step, "mmd_0002.pdf");

        var opener = markdown.IndexOf(Opener, StringComparison.Ordinal);
        var closer = markdown.IndexOf(Closer, StringComparison.Ordinal);
        Assert.True(opener >= 0 && closer > opener, "the step is not one keep-together unit");

        var inside = markdown[opener..closer];
        Assert.Contains("### The trap enters the kernel", inside);
        Assert.Contains("![The trap enters the kernel](mmd_0002.pdf)", inside);
    }

    [Fact]
    public void Step_EmitsRawLatexSoPandocPassesTheUnitThrough()
    {
        var markdown = PdfGeneratorService.BuildStepMarkdown(Step(), "mmd_0001.pdf");

        // A bare fence would reach LaTeX as a verbatim listing, so the unit has
        // to travel in a raw-LaTeX block.
        Assert.Contains("```{=latex}", markdown);
        Assert.DoesNotContain("```latex", markdown);
    }

    // --- Diagram heading travels with the first step ---

    private static LearningDiagram Diagram() => new()
    {
        Title = "One Inode, Five Pointers",
        Key = "vsfs-pointers",
        Description = "One inode points to four direct file-data blocks and an indirect pointer block.",
        Steps =
        {
            new LearningDiagramStep { Title = "Follow each pointer", Mermaid = "flowchart LR\n    A --> B" },
            new LearningDiagramStep { Title = "Read the pointer block", Mermaid = "flowchart LR\n    B --> C" }
        }
    };

    [Fact]
    public void Diagram_PutsHeadingAndDescriptionInsideFirstStepUnit()
    {
        var markdown = PdfGeneratorService.BuildDiagramSection(
            Diagram(), new[] { "mmd_0001.pdf", "mmd_0002.pdf" });

        var opener = markdown.IndexOf(Opener, StringComparison.Ordinal);
        var closer = markdown.IndexOf(Closer, StringComparison.Ordinal);
        Assert.True(opener >= 0 && closer > opener, "the first step is not a keep-together unit");

        var inside = markdown[opener..closer];
        Assert.Contains("## One Inode, Five Pointers", inside);
        Assert.Contains("One inode points to four direct file-data blocks", inside);
        Assert.Contains("### Follow each pointer", inside);
        Assert.Contains("![Follow each pointer](mmd_0001.pdf)", inside);
    }

    [Fact]
    public void Diagram_DoesNotRepeatHeadingInLaterStepUnits()
    {
        var markdown = PdfGeneratorService.BuildDiagramSection(
            Diagram(), new[] { "mmd_0001.pdf", "mmd_0002.pdf" });

        Assert.Equal(1, Count(markdown, "## One Inode, Five Pointers"));
        Assert.Equal(2, Count(markdown, Opener));
        Assert.Equal(2, Count(markdown, Closer));
    }

    [Fact]
    public void Diagram_WithoutSteps_StillEmitsItsHeading()
    {
        var diagram = Diagram();
        diagram.Steps.Clear();

        var markdown = PdfGeneratorService.BuildDiagramSection(diagram, Array.Empty<string>());

        Assert.Contains("## One Inode, Five Pointers", markdown);
        Assert.DoesNotContain(Opener, markdown);
    }

    private static int Count(string haystack, string needle)
    {
        var total = 0;
        for (var at = haystack.IndexOf(needle, StringComparison.Ordinal); at >= 0;
             at = haystack.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
            total++;
        return total;
    }
}
