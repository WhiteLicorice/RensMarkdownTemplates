namespace RensMarkdownTemplates.Models;

/// <summary>
/// Canonical frontmatter contract shared by authored course materials and PDF generation.
/// </summary>
public class MarkdownFrontMatter
{
    public string Title { get; set; } = "Untitled";
    public string Lead { get; set; } = "";
    public DateTime Published { get; set; } = DateTime.Now;
    public bool IsDraft { get; set; }
    public List<ArticleAuthor> Authors { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public string Subtitle { get; set; } = "";
    public DateTime? Deadline { get; set; }
    public bool NoDeadline { get; set; }
    public List<DateTime> ProgressReportDates { get; set; } = new();
    public List<DateTime> DefenseDates { get; set; } = new();
    public string? DownloadLink { get; set; }
    public List<SubmissionLink> Submissions { get; set; } = new();
    public List<LearningDiagram> Diagrams { get; set; } = new();
    public PdfConfig? Pdf { get; set; }
}

public class ArticleAuthor
{
    public const string DEFAULT_NAME = "Author";

    public string? Name { get; set; }
    public string? Nickname { get; set; }
    public string? GitHubUserName { get; set; }
}

public class SubmissionLink
{
    public string Name { get; set; } = "";
    public string Link { get; set; } = "";
}
