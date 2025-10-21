# Testing Guidelines

This repository uses helper builders and assertions to keep schedule tests readable, declarative, and precise.

## Patterns

- Build scenarios with `SchedulerScenario`:
  - `SchedulerScenario.For(date).AddTeacher(...).AddSupport(...).Run(_scheduler).ScheduleSelfCare(_scheduler);`

- Support timelines (exact):
  - Build with `ExpectedTimeline(date).ForSupport("S1") ... .End().Build();`
  - Assert with `TimelineAssertions.AssertSupportTimelineEquals(scenario.BySupport, "S1", expected["S1"]);`

- Teachers (exact):
  - Build with `ExpectedTeacherScheduleBuilder(date)` and times:
    - `.Coverage("T1", "11:00", 30)` etc.
  - Assert with `ExpectedScheduleAssertions.AssertTeacherTasksExactlyEqual(scenario.TeacherTasks, "T1", expected, startToleranceMinutes: 0);`

- Unscheduled:
  - Build with `ExpectedTimeline(date).ForUnscheduled()` (or `ForSupport("Unscheduled")`).
  - Assert with `TimelineAssertions.AssertUnscheduledTimelineEquals(scenario.BySupport, expected["Unscheduled"]);`

- Subsets and tolerance:
  - Presence-only subsets: `ExpectedScheduleBuilder` + `AssertEquivalent`.
  - Ordered time-tolerant subset: `TimelineAssertions.AssertSupportTimelineContainsInOrder(..., startToleranceMinutes: N)`.

## Do/Don’t

- Do: Express expectations in terms of timeline entries (Coverage/Lunch/Break/Free) with explicit start times and durations.
- Do: Use exact-equality assertions when you want to prohibit extras.
- Don’t: Manually enumerate and compare DateTimes when a builder + assertion exists.
- Don’t: Duplicate “contains” checks if you’re already doing exact-equality for the same items.

## Implementation Notes

- `CoverageTask.Minutes` is computed; set `Start`/`End`, not `Minutes`.
- .NET Framework 4.8: avoid deconstruction of `KeyValuePair<>`.
- Breaks typically include a 5-minute buffer via `BufferAfterMinutes`; expected timelines should reflect post-buffer idle start times accordingly (e.g., 10m break starting at 09:00 implies idle from 09:15).