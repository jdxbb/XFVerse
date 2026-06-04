# 2026-06-04 TV Library / Series Detail Regression Follow-up

## Goal

- Make media-library collection status filters include TV series when any visible season matches the selected state.
- Restore the agreed poster fallback and unidentified marking behavior in the series detail season list.

## Completed

- Series-level media-library items now aggregate visible season `IsFavorite`, `IsWantToWatch`, and `IsNotInterested` state.
- A TV series can appear in any selected collection-state combination when at least one visible season satisfies each selected state.
- Series detail season-list rows now fall back to the series poster when a season has no poster.
- Unidentified seasons append `（未识别）` to the displayed season name; if the season name is omitted as generic, the suffix is still shown next to the season label.
- Existing no-TMDB season rows also show `（未识别）` in the series season list, even if their persisted identification status is not `Failed`; this applies after reloading the detail page and does not require a rescan.
- Season detail titles now use the same unidentified suffix rule: valid season names become `第N季  A（未识别）`, and generic / omitted names become `第N季（未识别）` or `特别篇（未识别）`.

## Not Done

- No TV scan matching rule, AI rule, correction transaction, database schema, migration, database update, commit, or push was changed.
- Hidden seasons remain hidden from the media-library visible series aggregation.

## Verification

- `dotnet build MediaLibrary.sln`: passed, 0 warnings / 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`: empty.

## Known Issues

### Blocker

- None after build verification.

### Deferred

- More granular per-episode collection state remains outside the current TV season-state model.
- Runtime visual acceptance is still needed for long season names with the added unidentified suffix.

### Noise

- The series-level collection-state filter summarizes visible seasons; batch mode that expands to seasons still uses season-level rows directly.
