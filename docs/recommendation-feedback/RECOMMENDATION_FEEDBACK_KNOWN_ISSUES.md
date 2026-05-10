# Recommendation Feedback Known Issues

## Blocker

- No confirmed blocker after RF-2 build verification and 2026-05-10 recommendation-log validation.

## Deferred

- Recommendation-result immediate backfill after marking not interested is deferred; RF-1 removes the current card and waits for the next batch.
- Batch not-interested actions are deferred.
- Batch cancel-not-interested actions are deferred.
- Resource-library card / list visual "不想看" labels are deferred; RF-1.1 exposes the state through filtering and detail actions first.
- More complex negative-feedback summaries are deferred to a later recommendation-feedback iteration.
- Complex custom preference templates are deferred after RF-2.
- Multiple custom preference profiles are deferred after RF-2.
- User-profile / statistics fusion is deferred after RF-2.
- Preference history is deferred after RF-2.
- Preference import / export is deferred after RF-2.
- Enabling or changing custom recommendation preferences can make the next refresh miss previous cache / candidate-pool combinations because the fingerprint changes. This is expected, but remains a UX observation point if users perceive the first refresh as slow.
- Expanding the not-interested prompt summary to as many as 200 records can increase prompt length and first AI wait time when users have many negative-feedback records. Local hard filtering remains full and is not capped by this prompt summary.
- Title-year fallback false positives remain an observation item; RF-1 logs `matchKey=title-year` to make manual review possible.
- Final UI refactoring is deferred and out of scope for this stage.
- User profiling and statistics charts are deferred and out of scope for this stage.
- Movie discovery and search are deferred and out of scope for this stage.
- TV-series support is deferred and out of scope for this stage.
- Local-folder support is deferred and out of scope for this stage.
- Online subtitle search is deferred and out of scope for this stage.
- Additional database fields and migrations are deferred unless a plan phase proves they are required, compares alternatives, records the reason, and receives confirmation first. RF-1 used one approved migration for `UserMovieCollectionItem.IsNotInterested`.

## Noise

- Player issues, R2.1a, mpv, subtitles, audio tracks, and cache behavior are not part of this stage.
- Library Batch Ops issues are not part of this stage.
- Scan-main-flow behavior is not part of this stage.
- AI prompt-only filtering for not-interested items is not acceptable for RF-1; local state and local filtering are required.
- Not-interested filtering diagnostics are expected in the AI performance log and may add bounded noise during manual verification.
- RF-2 custom preference diagnostics may log enabled state, text length, and a short hash, but must not log full preference text or the full AI prompt.
- RF-2 prompt-injection safety does not rely on the model fully obeying user text. User preference text is treated as soft taste input only; local hard filters remain the enforcement layer for not-interested, watched-state, scope, and safety rules.
- 2026-05-10 log validation found the RF-1 / RF-2 diagnostics normal. Candidate-pool refill cancellation with `foreground-request` and request cancellation with `TaskCanceledException` are expected coordination noise when foreground refreshes supersede in-flight work.

## Maintenance Rules

- Record only issues supported by logs, code paths, or manual validation.
- Do not record temporary guesses as confirmed facts.
- Move resolved issues into the stage log or mark their resolved sub-stage here.
- Keep player-stage issues in the player documentation, not in this stage.
- Keep Library Batch Ops issues in the Library Batch Ops documentation, not in this stage.
