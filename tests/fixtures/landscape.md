---
title: Shared pipeline fixture
subtitle: Landscape markers
published: 2026-08-05
tags: [fixture]
authors:
  - name: Rene Andre Bedonia Jocsing
---

# Portrait before

This page is upright. The marker below is what rotates the next one, and the
marker after it is what turns rotation back off.

A marker shown inside a code sample stays literal, because Pandoc hands a fenced
block to the filter as code rather than as raw HTML:

```markdown
<!-- landscape-start -->
```

<!-- landscape-start -->

## Rotated region

Headings and prose rotate along with the table, which is the whole point of
marking a region instead of marking one table.

| Criterion | Excellent | Good | Poor |
|---|---|---|---|
| Coverage | Every case | Most cases | Few cases |
| Clarity | Reads cleanly | Readable | Hard to follow |

<!-- landscape-end -->

# Portrait after

This page is upright again. If it isn't, the region failed to close.

A wrapped table draws in the ruled schedule form without turning the page,
which is the point of keeping the two decisions apart:

::: {.study-schedule}
## Ruled but upright

| No. | Topics | Learning Outcomes | COs | Activities | Assessment |
|:--:|:---|:---|:--:|:---|:---|
| 1 | Course introduction | Recognize the syllabus | CO1 | Discussion | Quiz |
:::
