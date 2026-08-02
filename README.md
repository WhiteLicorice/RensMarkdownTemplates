# RensMarkdownTemplates

This repository owns the Markdown-to-PDF pipeline shared by
[Ren's Courses](https://github.com/WhiteLicorice/Ren-s-Courses) and the private
course-materials workspace. It contains the .NET generator, command-line entrypoint,
Pandoc templates and Lua filters, pinned Pandoc and Tectonic manifests, and the
Mermaid CLI configuration. Consumer repositories include it as a pinned Git
submodule rather than copying these files.

The repository is public so GitHub Actions can initialize the dependency without a
cross-repository credential. It is proprietary, not open source. See
[`LICENSE.md`](LICENSE.md) before using the code, templates, or institutional assets.

## Requirements

- .NET 9 SDK
- Node.js and npm
- Windows x64 or Linux x64

The first build runs `npm ci`. The generator downloads the pinned Pandoc and Tectonic
archives after verifying their SHA-256 checksums. It keeps those tools, Puppeteer's
browser, Tectonic's package cache, and generated-document state under the configured
artifacts directory.

## Render one document

```powershell
dotnet run --project src/RensMarkdownTemplates.Cli -- render `
  --input path/to/material.md `
  --output path/to/material.pdf `
  --content-root path/to/consumer `
  --cache-root path/to/consumer/.cache `
  --pipeline-root .
```

Add `--force` to invalidate that document's generated-PDF cache before rendering.
Repository-wide consumers should call `scripts/Convert-All.ps1`, which retains the
course-materials repository's Git-aware discovery, `.pdfignore`, `<!--no-pdf-->`,
Marp exclusions, adjacent output, and source/pipeline hash cache.

## Canonical frontmatter

Both consumers accept the same YAML frontmatter. Fields used only by the course site
remain valid when a document is rendered directly.

```yaml
---
title: Example Laboratory
subtitle: CMSC 125 Laboratory Manual 1
lead: A short web-facing summary.
published: 2026-08-01
deadline: 2026-08-08
tags: [cmsc-125]
authors:
  - name: Rene Andre Bedonia Jocsing
    gitHubUserName: WhiteLicorice
    nickname: Ren
isDraft: false
submissions:
  - name: Source code
    link: https://example.com/submission
pdf:
  template: default
  variables:
    documentType: Course Syllabus
    courseCode: CMSC 125
    academicTerm: 2nd Semester, A.Y. 2025-2026
    meetingSchedule: "Lecture: MTh 9:00-11:00 AM"
    venue: MILC
---
```

The `pdf` block is optional. `pdf.template` selects a directory under `templates/` and
defaults to `default`. Entries in `pdf.variables` become
`$pdf.variables.<name>$` values in the Pandoc template.

## Mermaid diagrams

Mermaid uses the same frontmatter and marker contract on the website and in generated
PDFs. Each step contains a complete Mermaid definition. Put the diagram marker on its
own line where the diagram belongs.

```yaml
diagrams:
  - title: Bubble sort
    key: bubble-sort-pass
    description: Follow one pass through the array.
    steps:
      - title: Compare the first pair
        description: Five is greater than two, so the values are out of order.
        mermaid: |
          flowchart LR
              A[5] --> B[2]
```

```markdown
<!-- diagram: bubble-sort-pass -->
```

Keys use lower-case kebab case. Markers inside fenced code blocks remain literal.
Unreferenced diagrams are not rendered. Repeating a marker repeats the diagram, and the
first declaration wins when frontmatter contains a duplicate key. A Mermaid failure
fails that document without preventing the course site from generating its other PDFs.

## Templates and tests

The default template supports ordinary course materials and the formal syllabus
variables shown above. It also handles syntax-colored code, automatically and explicitly
rotated tables, rubric sections, institutional assets, and standalone `<!-- newpage -->`
markers.

Run the maintained unit gate with:

```powershell
dotnet test tests/RensMarkdownTemplates.Tests/RensMarkdownTemplates.Tests.csproj
```

For the full toolchain path, render `tests/fixtures/mermaid.md` with the CLI and confirm
that the output starts with the `%PDF-` signature. CI runs both checks on Linux.

## Updating consumer pins

Change the pipeline here first and run its tests. After the commit is available on
GitHub, update each consumer's submodule pin and run that consumer's documented build.
Do not copy a template, filter, tool manifest, or generator class back into a consumer.
