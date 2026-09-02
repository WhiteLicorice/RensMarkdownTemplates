namespace RensMarkdownTemplates.Models;

public class LearningDiagram
{
    public string Title { get; set; } = "Untitled diagram";
    public string Key { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>
    /// Optional author-approved vertical flow direction for narrow viewports.
    /// Only "TB" and "BT" are supported, and only Mermaid flowchart or graph
    /// steps can use it. The web renderer applies it when the canonical
    /// diagram cannot stay readable at the available width. Generated PDFs
    /// always keep the canonical <see cref="LearningDiagramStep.Mermaid"/>.
    /// </summary>
    public string NarrowDirection { get; set; } = "";

    public List<LearningDiagramStep> Steps { get; set; } = new();
}
