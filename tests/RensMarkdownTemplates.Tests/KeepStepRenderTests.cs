using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;

namespace RensMarkdownTemplates.Tests;

/// <summary>
/// The keep-together unit has to hold a step that fits on one page, even when
/// the diagram heading above it pushes the pair past one page. These tests
/// render the template's own unit with the pinned Tectonic and read the page
/// numbers back from the auxiliary file, so they measure the page and not the
/// source text.
/// </summary>
public class KeepStepRenderTests
{
    // The text block of the default template at 11 pt is 745.315 pt tall. The
    // diagram heading and its description of the document below add 62.85 pt,
    // so a first step of 727.13 pt fits on a page alone and does not fit under
    // its heading.
    private const double TextBlockPt = 745.315;

    [Fact]
    public void FirstStep_ThatFitsOnOnePage_StaysWholeUnderItsDiagramHeading()
    {
        // The step alone is 727.13 pt and fits on a page with 18.2 pt to spare.
        // The diagram heading and its description add 62.85 pt, so the pair is
        // 789.98 pt and exceeds one page by 44.7 pt. The step still has to
        // reach one page whole.
        var render = Render("fits-under-heading", stepPt: 640);

        Assert.Equal(render.Page("keepstep-start"), render.Page("keepstep-end"));
    }

    [Fact]
    public void DiagramHeading_StaysWithItsFirstStep_WhenTheTwoFitOnOnePage()
    {
        // The step alone is 647.13 pt and the pair is 709.98 pt, so the two fit
        // on one page together. The heading must not sit at a page foot with
        // its step overleaf.
        var render = Render("heading-with-step", stepPt: 560);

        var heading = render.Page("keepstep-heading");
        Assert.Equal(heading, render.Page("keepstep-start"));
        Assert.Equal(heading, render.Page("keepstep-end"));
    }

    [Fact]
    public void StepTallerThanOnePage_StillRendersWithoutLoss()
    {
        // The step alone is 746.99 pt, taller than the 745.315 pt text block,
        // so it has to break inside itself. Nothing may go missing.
        var render = Render("taller-than-page", stepPt: 660);

        Assert.True(render.Labels.ContainsKey("keepstep-start"), "the step heading was not typeset");
        Assert.True(render.Labels.ContainsKey("keepstep-end"), "the end of the step was not typeset");
        Assert.True(render.Labels.ContainsKey("keepstep-after"), "the text after the diagram was not typeset");
    }

    [Fact]
    public void DiagramHeading_DoesNotSitAloneAtThePageFoot_WhenTheStepCannotShareItsPage()
    {
        // The step alone is 727.13 pt and fits on a page with 18.2 pt to spare.
        // The diagram heading and its description add 62.85 pt, so the pair is
        // 789.98 pt and exceeds one page by 44.7 pt. A 624 pt filler puts the
        // heading's last line at y=778.8 of the 793.04 pt text block, 14 pt
        // above the foot. The heading must not stay there with its step
        // overleaf. It starts a new page, and the step keeps the next page
        // whole.
        var render = Render("heading-at-foot", stepPt: 640, fillerPt: 624);

        Assert.True(
            PageNumber(render, "keepstep-heading") > PageNumber(render, "keepstep-before"),
            "the diagram heading stayed on the page above its step, at the page foot");
        Assert.Equal(render.Page("keepstep-start"), render.Page("keepstep-end"));
    }

    [Fact]
    public void DiagramHeading_StartsANewPage_WhenTheHeadingAndTheStepExceedOnePage()
    {
        // The reviewer's r4-g-620 case. The step alone is 707.13 pt and fits on
        // a page with 38.2 pt to spare. The diagram heading and its description
        // add 62.85 pt, so the pair is 769.98 pt and exceeds one page by 24.7
        // pt. The step wins: it stays whole on one page, and the heading starts
        // a new page instead of staying at the foot of the page above.
        var render = Render("heading-over-page", stepPt: 620);

        Assert.Equal(render.Page("keepstep-start"), render.Page("keepstep-end"));
        Assert.True(
            PageNumber(render, "keepstep-heading") > PageNumber(render, "keepstep-before"),
            "the diagram heading stayed on the page above its step, at the page foot");
    }

    // --- Rendering ---

    private sealed record RenderResult(string Log, IReadOnlyDictionary<string, string> Labels)
    {
        public string Page(string label) =>
            Labels.TryGetValue(label, out var page)
                ? page
                : throw new Xunit.Sdk.XunitException(
                    $"the render wrote no page for {label}; log:\n{Log}");
    }

    private static int PageNumber(RenderResult render, string label) =>
        int.Parse(render.Page(label), System.Globalization.CultureInfo.InvariantCulture);

    private static RenderResult Render(string name, double stepPt, int fillerPt = 0)
    {
        var root = RepositoryRoot();
        var workDir = Path.Combine(root, "artifacts", "keepstep-render", name);
        Directory.CreateDirectory(workDir);

        var texPath = Path.Combine(workDir, "document.tex");
        File.WriteAllText(texPath, Document(stepPt, fillerPt), new UTF8Encoding(false));

        var cacheDir = Path.Combine(root, "artifacts", "tectonic-cache");
        Directory.CreateDirectory(cacheDir);

        var start = new ProcessStartInfo(Tectonic(root))
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in new[]
                 {
                     "-X", "compile", "--untrusted", "--keep-logs", "--keep-intermediates",
                     "--bundle", ToolchainManifest.Current.Tectonic.BundleUrl!, texPath
                 })
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment["TECTONIC_CACHE_DIR"] = cacheDir;

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();

        var logPath = Path.Combine(workDir, "document.log");
        var log = File.Exists(logPath) ? File.ReadAllText(logPath) : output;
        Assert.True(process.ExitCode == 0, $"Tectonic exit {process.ExitCode}:\n{output}\n{log}");

        // The heights in this file assume the text block of the default
        // template at 11 pt. A geometry change has to reach this file.
        var goal = Regex.Match(log, @"KEEPSTEP goal=([0-9.]+)pt");
        Assert.True(goal.Success, $"the render reported no page goal; log:\n{log}");
        Assert.Equal(TextBlockPt, double.Parse(goal.Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture), 3);

        var auxPath = Path.Combine(workDir, "document.aux");
        Assert.True(File.Exists(auxPath), $"the render wrote no auxiliary file; log:\n{log}");

        return new RenderResult(log, Labels(File.ReadAllText(auxPath)));
    }

    private static IReadOnlyDictionary<string, string> Labels(string aux)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(aux, @"\\newlabel\{([^}]*)\}\{\{[^}]*\}\{([^}]*)\}"))
        {
            labels[match.Groups[1].Value] = match.Groups[2].Value;
        }
        return labels;
    }

    /// <summary>
    /// The document the generator writes for one diagram: a paragraph, the
    /// diagram heading and its description, then one step. The heading sits
    /// in its own unit when the template defines one, and the fallback keeps
    /// the document identical against a template that has no such unit.
    /// <paramref name="fillerPt"/> adds a rule above the diagram, so a test can
    /// put the diagram heading at a chosen height on the page.
    /// </summary>
    private static string Document(double stepPt, int fillerPt) =>
        """
        \documentclass[a4paper,11pt]{article}
        \usepackage[left=0.68in,right=0.68in,top=0.70in,bottom=0.68in]{geometry}
        \usepackage{titlesec}
        \titleformat{\section}{\normalfont\large\bfseries}{}{0pt}{}
        \titleformat{\subsection}{\normalfont\normalsize\bfseries}{}{0pt}{}
        \titleformat{\subsubsection}{\normalfont\normalsize\itshape\bfseries}{}{0pt}{}
        \titlespacing*{\section}{0pt}{1.25em}{0.55em}
        \titlespacing*{\subsection}{0pt}{0.9em}{0.32em}
        \titlespacing*{\subsubsection}{0pt}{0.7em}{0.25em}
        \setlength{\parindent}{0pt}
        \setlength{\parskip}{0.48em}
        UNIT
        \makeatletter
        \@ifundefined{keephead}{\newenvironment{keephead}{}{}}{}
        \makeatother
        \begin{document}

        \section{Before}

        One short paragraph before the diagram.\label{keepstep-before}

        FILLER

        \begin{keepstep}

        \begin{keephead}

        \subsection{A diagram with a long description}\label{keepstep-heading}

        This diagram description is long enough to take several lines on the page. It
        exists so the diagram heading and its description add real height to the first
        step's keep-together unit.

        \end{keephead}

        \subsubsection{The only step}\label{keepstep-start}

        This step fits on one page by itself. The unit that holds it does not fit,
        because the diagram heading and its description sit inside the same unit.

        \rule{0.6\linewidth}{STEPPTpt}

        Figure 1: The only step\label{keepstep-end}

        \end{keepstep}

        \subsection{After the diagram}\label{keepstep-after}

        One short paragraph after the diagram.

        \typeout{KEEPSTEP goal=\the\pagegoal}

        \end{document}
        """
            .Replace("UNIT", Unit())
            .Replace("FILLER", fillerPt > 0 ? $@"\rule{{0.6\linewidth}}{{{fillerPt}pt}}" : "")
            .Replace("STEPPT", stepPt.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// The keep-together unit, read from the default template so the test
    /// measures the shipped definition and not a copy of it.
    /// </summary>
    private static string Unit()
    {
        var template = Read(Path.Combine(RepositoryRoot(), "templates", "default", "template.latex"));
        var start = template.IndexOf("\\newsavebox{\\keepstepbox}", StringComparison.Ordinal);
        Assert.True(start >= 0, "the default template does not define the keep-together unit");

        var environment = template.IndexOf("\\newenvironment{keepstep}", start, StringComparison.Ordinal);
        Assert.True(environment >= 0, "the default template does not define the keepstep environment");

        var end = template.IndexOf("\n\n", environment, StringComparison.Ordinal);
        return end < 0 ? template[start..] : template[start..end];
    }

    private static string Tectonic(string root)
    {
        var rid = OperatingSystem.IsWindows() ? "win-x64" : "linux-x64";
        var manifest = ToolchainManifest.Current.Tectonic;
        var pinned = Path.Combine(root, "artifacts", "pdf-toolchain", "tectonic",
            manifest.Version, rid, manifest.Archives[rid].ExecutablePath);
        if (File.Exists(pinned))
        {
            return pinned;
        }

        // A fresh clone has no toolchain yet. The pipeline bootstraps the same
        // pinned binaries, so the test asks for the same bootstrap.
        var provider = new ToolchainProvider(new PdfGeneratorOptions
        {
            ContentRoot = root,
            PipelineRoot = root
        });
        if (provider.BootstrapAsync(NullLogger.Instance).GetAwaiter().GetResult() &&
            provider.TectonicPath is { } bootstrapped)
        {
            return bootstrapped;
        }

        throw new InvalidOperationException(
            $"the pinned Tectonic is not at {pinned} and could not be bootstrapped");
    }

    private static string Read(string path) =>
        File.ReadAllText(path).Replace("\r\n", "\n");

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RensMarkdownTemplates.sln")) ||
                Directory.Exists(Path.Combine(directory.FullName, "templates")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("the repository root was not found above the test binary");
    }
}
