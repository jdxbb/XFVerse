# UI Loading State Guidelines

This document records reusable loading-state rules for later page UI refactors.

## Page Initial Loading

- A page that needs data before its stable layout can be rendered must start in loading state from the first binding read.
- Initial loading must not render empty cards, default buttons, disabled-looking controls, or placeholder business content before data is applied.
- The page body should be collapsed while initial loading is active.
- The loading layer should be transparent and hit-test blocking, with only a centered spinner and one short status line.
- The loading layer must not add a solid card, tinted mask, or disabled-state background.
- Once the first stable model is applied, the loading layer is hidden and the normal page body is shown.
- Later lightweight refreshes may keep existing content visible, but navigation to a different detail entity must clear stale content before the new model loads.

## Media Library Initial Loading

- The media-library content area uses an empty transparent loading surface, centered in the result region.
- The visible loading content is only a spinner plus a small `加载中` label.
- Do not use the empty-state card style for initial library loading. Empty-state cards are reserved for completed queries with no results.

## Poster Loading

- Poster placeholders keep the existing poster-placeholder image as their background.
- While a poster request is active, the placeholder shows a spinner, not `无海报`.
- `无海报` is shown only when the bound poster source is empty or the current poster request has failed after its available attempts.
- Rebinding or re-entering a page starts from the loading state again before the new request result is known.
- Do not use failed-download cooldown to replace poster request attempts; the no-poster label belongs after the current request path has no usable image.
- Do not add extra poster loading backgrounds, cards, or masks; the spinner sits directly over the poster placeholder image.

## Verification Checklist

- Cold-open page: no half-styled layout appears before the spinner.
- Empty query result: empty-state copy appears only after loading completes.
- Poster with valid URL: spinner appears first, then the image.
- Poster retry guard: first failure and later page re-entry both go through the poster-cache request path before settling on the no-poster label.
- Poster without URL: `无海报` appears.
- Poster request failure: spinner appears during the request, then `无海报`.
