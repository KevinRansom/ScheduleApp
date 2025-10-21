# Copilot Instructions – WPF Styling Rules

## General Principles
- Always treat design requests as **holistic visual systems**, not isolated tweaks.
- When a style change is requested, apply it consistently to **all visual layers**: outer container, headers, cells, gridlines, and backgrounds.
- Prioritize **pixel-perfect alignment** and visual harmony over default WPF templates.

## Rounded Corners
- When a DataGrid is given rounded corners, ensure:
  - Outer `Border` has the specified `CornerRadius`.
  - All child elements (headers, cells, gridlines, backgrounds) are clipped to the same rounded geometry.
  - First and last column headers have matching rounded top corners; all other headers remain square.
  - No gridline, background, or cell border extends beyond the rounded boundary.

## Gridlines and Borders
- Disable default gridlines with `GridLinesVisibility="None"`.
- Draw custom cell borders via `CellStyle`:
  - Apply right border to all cells except the last column.
  - Apply bottom border to all cells except the last row.
- Ensure borders align exactly with the outer container’s edges and corner radius.

## Header Styling
- Match header background and gradient to the body cells for a unified look.
- Remove default WPF header chrome.
- Apply consistent padding and alignment between headers and cells.

## Backgrounds and Gradients
- Use subtle vertical gradients unless otherwise specified.
  - Default: `#F0F0F0` (top) → `#E0E0E0` (bottom).
- Apply the same gradient logic to headers and cells for consistency.

## Alignment and Spacing
- Set `SnapsToDevicePixels="True"` on all containers.
- Use integer values for `BorderThickness` and `Margin` to avoid anti-aliasing artifacts.
- Keep `CellPadding` and `ColumnHeaderPadding` identical unless otherwise specified.
- Maintain consistent `RowHeight` for vertical rhythm.

## Implementation Notes
- Avoid relying on default `DataGrid` templates; override as needed to meet these rules.
- When in doubt, **clip to a rounded rectangle** at the container level to enforce boundaries.
- Always verify that visual changes render correctly in both normal and print layouts.

# Test Authoring Rules

Use these conventions for all schedule-related tests.

- Scenario setup
  - Build inputs with `SchedulerScenario.For(date)` and chain `.AddTeacher(...)`, `.AddSupport(...)`, then `.Run(_scheduler)`. Call `.ScheduleSelfCare(_scheduler)` when validating support self-care placement.
- Support timelines (exact)
  - Build with `new ExpectedTimeline(date).ForSupport("<SupportName>")` and chain entries:
    - `Coverage("<Teacher>", "HH:mm", minutes)`, `Lunch("HH:mm")`, `Break("HH:mm")`, `Free("HH:mm", minutes)`, then `.End().Build()`.
  - Assert with `TimelineAssertions.AssertSupportTimelineEquals(scenario.BySupport, "<SupportName>", expected["<SupportName>"]);`
- Teachers (exact)
  - Build with `new ExpectedTeacherScheduleBuilder(date)` and add:
    - `.Coverage("<Teacher>", "HH:mm", minutes)`
  - Assert with `ExpectedScheduleAssertions.AssertTeacherTasksExactlyEqual(scenario.TeacherTasks, "<Teacher>", expected, startToleranceMinutes: 0);`
- Unscheduled bucket
  - Prefer `ExpectedTimeline(date).ForUnscheduled()` (or `ForSupport("Unscheduled")`) and assert with:
    - `TimelineAssertions.AssertUnscheduledTimelineEquals(scenario.BySupport, expected["Unscheduled"]);`
- Subset checks and tolerance
  - If exact timing is flexible, use:
    - `ExpectedScheduleBuilder` + `AssertEquivalent` for presence-only subsets, or
    - `TimelineAssertions.AssertSupportTimelineContainsInOrder(..., startToleranceMinutes: N)` for ordered, time-tolerant subsets.
- Avoid redundancy
  - When asserting exact equality, do not add extra “contains” or manual DateTime checks for the same items.
- Implementation constraints (C# 8 / .NET Framework 4.8)
  - Do not assign `CoverageTask.Minutes` (it is computed from `End - Start`).
  - Avoid tuple deconstruction for `KeyValuePair<>`; iterate as `foreach (var kvp in dict)`.
  - Prefer Start/End setting; keep any buffers via `BufferAfterMinutes` as needed.

