namespace RensMarkdownTemplates.Tests;

/// <summary>
/// The keep-together unit lives in the template, not in the generated Markdown.
/// These tests read the template and check the unit against the page it sits on.
/// </summary>
public class KeepStepTemplateTests
{
    private const string EnvironmentName = "keepstep";

    [Fact]
    public void KeepStep_DoesNotAddInfiniteGlue()
    {
        // Problem Set 1 renders 42 pages before the unit existed and 43 after.
        // All five of its steps already sat on one page, so the unit gained
        // nothing there. The cause is infinite glue: \filbreak is
        // \par\vfil\penalty-200\vfilneg, and the \vfil makes the step start the
        // cheapest breakpoint on the page. TeX then breaks there whenever the
        // page overflows anywhere after the step. A step that fits has to stay
        // on its page, so the unit must not add infinite glue.
        var body = EnvironmentBody(EnvironmentName);

        Assert.DoesNotContain("\\vfil", body);
        Assert.DoesNotContain("\\filbreak", body);
    }

    [Fact]
    public void KeepStep_MeasuresTheStepBeforeItBreaks()
    {
        // The unit boxes the step, compares the height of the box with the
        // space left on the page, and breaks only when the step fits on one
        // page and the space left is smaller. The box contents then go back on
        // the page with \unvbox, so a step taller than one page still breaks
        // inside itself.
        var body = EnvironmentBody(EnvironmentName);

        Assert.Contains("\\pagegoal", body);
        Assert.Contains("\\unvbox", body);
    }

    [Fact]
    public void EveryTemplate_DefinesTheKeepStepEnvironment()
    {
        // The generator writes \begin{keepstep} into every diagram step. A
        // template that does not define the environment fails in LaTeX with an
        // undefined environment, so each template has to carry the definition.
        AssertEveryTemplateDefines(EnvironmentName);
    }

    [Fact]
    public void EveryTemplate_DefinesTheKeepHeadEnvironment()
    {
        // The generator writes \begin{keephead} around the diagram heading of
        // the first step, so the template can measure the step apart from the
        // heading. A template without the environment fails in LaTeX.
        AssertEveryTemplateDefines("keephead");
    }

    private static void AssertEveryTemplateDefines(string name)
    {
        var templates = Directory.GetDirectories(Path.Combine(RepositoryRoot(), "templates"));

        Assert.NotEmpty(templates);
        foreach (var directory in templates)
        {
            var template = Path.Combine(directory, "template.latex");
            Assert.True(File.Exists(template), $"{template} is missing");
            Assert.Contains($"\\newenvironment{{{name}}}", Read(template));
        }
    }

    private static string EnvironmentBody(string name)
    {
        var text = Read(Path.Combine(RepositoryRoot(), "templates", "default", "template.latex"));
        var start = text.IndexOf($"\\newenvironment{{{name}}}", StringComparison.Ordinal);
        Assert.True(start >= 0, $"the default template does not define the {name} environment");

        var end = text.IndexOf("\n\n", start, StringComparison.Ordinal);
        return end < 0 ? text[start..] : text[start..end];
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
