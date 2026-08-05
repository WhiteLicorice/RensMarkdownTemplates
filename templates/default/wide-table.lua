-- Shared structural rendering for formal syllabi and ordinary materials.
--
-- Two separate decisions, one trigger each. A ::: {.study-schedule} div draws
-- its tables in the OBE form's ruled, shaded, repeating-header style. A
-- <!-- landscape-start --> / <!-- landscape-end --> pair turns the pages
-- between them landscape. Nothing else rotates a page, so when a PDF comes out
-- sideways there is exactly one place to look for the reason.

local function render_blocks(blocks)
  local rendered = pandoc.write(pandoc.Pandoc(blocks), "latex")
  local cleaned = rendered:gsub("%s+$", "")
  return cleaned
end

local function cell_latex(cell)
  local rendered = render_blocks(cell.contents)
  rendered = rendered:gsub(
    "Formative:",
    "\\textbf{Formative:}"
  )
  rendered = rendered:gsub(
    "Summative:",
    "\\textbf{Summative:}"
  )
  rendered = rendered:gsub(
    "(Passing grade[^%.]*%.)",
    "\\emph{%1}"
  )
  rendered = rendered:gsub(
    "(Passing Grade[^%.]*%.)",
    "\\emph{%1}"
  )
  return rendered
end

local function rows_from(table_element)
  local rows = {}
  for _, body in ipairs(table_element.bodies) do
    for _, row in ipairs(body.body) do
      table.insert(rows, row)
    end
  end
  for _, row in ipairs(table_element.foot.rows) do
    table.insert(rows, row)
  end
  return rows
end

local function schedule_table(table_element, title)
  local columns = #table_element.colspecs
  local widths
  if columns == 7 then
    widths = {0.060, 0.085, 0.180, 0.220, 0.070, 0.230, 0.150}
  elseif columns == 6 then
    widths = {0.080, 0.220, 0.230, 0.080, 0.235, 0.145}
  else
    return render_blocks({table_element})
  end

  local specs = {}
  for index, width in ipairs(widths) do
    local alignment = (
      index <= 2 or
      (columns == 7 and index == 5) or
      (columns == 6 and index == 4)
    ) and "\\centering\\arraybackslash" or "\\raggedright\\arraybackslash"
    table.insert(
      specs,
      string.format(
        ">{%s}p{\\dimexpr %.3f\\linewidth-2\\tabcolsep\\relax}",
        alignment,
        width
      )
    )
  end

  local header_cells = table_element.head.rows[1].cells
  local header = {}
  for _, cell in ipairs(header_cells) do
    table.insert(
      header,
      "{\\centering\\bfseries " .. cell_latex(cell) .. "\\par}"
    )
  end
  local header_row = table.concat(header, " & ")
  local table_title = nil
  if title ~= nil and title ~= "" then
    table_title = string.format(
      "\\multicolumn{%d}{@{}l@{}}{\\normalsize\\bfseries %s}"
        .. " \\\\[1.0ex]",
      columns,
      title
    )
  end
  local green_bar = string.format(
    "\\multicolumn{%d}{@{}l@{}}{\\makebox[0pt][l]{\\hspace{-0.60in}"
      .. "\\color{upgreen}\\rule{297mm}{0.055in}}} \\\\[-0.8ex]",
    columns
  )
  local maroon_bar = string.format(
    "\\multicolumn{%d}{@{}l@{}}{\\makebox[0pt][l]{\\hspace{-0.60in}"
      .. "\\color{upmaroon}\\rule{297mm}{0.26in}}} \\\\",
    columns
  )

  -- The first page carries no running header of its own. Whatever placed the
  -- table there printed one already: a landscape region opens with
  -- \LandscapeContentHeader, and an upright page has the ordinary fancyhdr
  -- header. Continuation pages are the ones that need it, so \endhead below
  -- keeps its copy.
  local output = {
    "\\begingroup",
    "\\small",
    "\\setlength{\\tabcolsep}{4pt}",
    "\\renewcommand{\\arraystretch}{1.20}",
    "\\arrayrulecolor{black!28}",
    "\\begin{longtable}{|" .. table.concat(specs, "|") .. "|}",
  }
  if table_title ~= nil then
    table.insert(output, table_title)
  end
  table.insert(output, "\\hline")
  table.insert(
    output,
    "\\rowcolor{tablegray}[\\tabcolsep]"
      .. "[\\dimexpr2\\tabcolsep\\relax]\\rule{0pt}{3.6ex}"
      .. header_row .. " \\\\[0.8ex]"
  )
  table.insert(output, "\\hline")
  table.insert(output, "\\endfirsthead")
  table.insert(output, "\\hline")
  table.insert(
    output,
    "\\rowcolor{tablegray}[\\tabcolsep]"
      .. "[\\dimexpr2\\tabcolsep\\relax]\\rule{0pt}{3.6ex}"
      .. header_row .. " \\\\[0.8ex]"
  )
  table.insert(output, "\\hline")
  table.insert(output, "\\endhead")
  table.insert(output, "\\hline")
  table.insert(output, green_bar)
  table.insert(output, maroon_bar)
  table.insert(output, "\\endfoot")
  table.insert(output, "\\hline")
  table.insert(output, green_bar)
  table.insert(output, maroon_bar)
  table.insert(output, "\\endlastfoot")

  for _, row in ipairs(rows_from(table_element)) do
    local cells = {}
    for _, cell in ipairs(row.cells) do
      table.insert(cells, cell_latex(cell))
    end
    table.insert(output, table.concat(cells, " & ") .. " \\\\[0.9ex]")
    table.insert(output, "\\hline")
  end

  table.insert(output, "\\end{longtable}")
  table.insert(output, "\\endgroup")
  return table.concat(output, "\n")
end

local function has_class(div, expected)
  for _, class in ipairs(div.classes) do
    if class == expected then
      return true
    end
  end
  return false
end

-- Landscape regions are delimited the way page breaks and diagrams are, with a
-- marker on its own line. A marker shown inside a fenced code block reaches the
-- filter as a CodeBlock rather than a RawBlock, so it stays literal on its own.
local landscape_start = "^<!%-%-%s*landscape%-start%s*%-%->$"
local landscape_end = "^<!%-%-%s*landscape%-end%s*%-%->$"

local function is_marker(block, pattern)
  if block.t ~= "RawBlock" or block.format ~= "html" then
    return false
  end
  return block.text:lower():match(pattern) ~= nil
end

-- The ruled, shaded, repeating-header form from the OBE study schedule. This
-- picks how a table is drawn and says nothing about which way the page faces;
-- rotating it is the markers' job.
local function ruled_table(table_element, title)
  return pandoc.RawBlock("latex", schedule_table(table_element, title))
end

local transform_blocks

local function landscape_region(blocks)
  return {
    pandoc.RawBlock(
      "latex",
      "\\SyllabusLandscapeBegin\n\\begingroup\n\\small"
    ),
    pandoc.RawBlock("latex", render_blocks(transform_blocks(blocks))),
    pandoc.RawBlock(
      "latex",
      "\\endgroup\n\\SyllabusLandscapeEnd"
    ),
  }
end

transform_blocks = function(blocks)
  local output = {}
  local index = 1
  while index <= #blocks do
    local block = blocks[index]
    if is_marker(block, landscape_start) then
      local region = {}
      local closed = false
      index = index + 1
      while index <= #blocks do
        local candidate = blocks[index]
        index = index + 1
        if is_marker(candidate, landscape_end) then
          closed = true
          break
        end
        if is_marker(candidate, landscape_start) then
          error(
            "<!-- landscape-start --> inside an open landscape region; "
              .. "close the first one before opening another"
          )
        end
        table.insert(region, candidate)
      end
      if not closed then
        error(
          "<!-- landscape-start --> without a matching <!-- landscape-end -->"
        )
      end
      for _, replacement in ipairs(landscape_region(region)) do
        table.insert(output, replacement)
      end
    elseif is_marker(block, landscape_end) then
      error(
        "<!-- landscape-end --> without a matching <!-- landscape-start -->"
      )
    elseif block.t == "Div" and has_class(block, "study-schedule") then
      local title = nil
      for _, child in ipairs(block.content) do
        if child.t == "Header" and title == nil then
          title = render_blocks({pandoc.Plain(child.content)})
        elseif child.t == "Table" then
          table.insert(output, ruled_table(child, title))
          title = nil
        else
          table.insert(output, child)
        end
      end
      index = index + 1
    elseif block.t == "Div" then
      block.content = transform_blocks(block.content)
      table.insert(output, block)
      index = index + 1
    else
      table.insert(output, block)
      index = index + 1
    end
  end
  return output
end

function Pandoc(document)
  if FORMAT:match("latex") then
    document.blocks = transform_blocks(document.blocks)
  end
  return document
end
