# Autonomy Log

A running record of what the scheduled autonomous hardening loop has done, and the guardrails
it operates under. Each firing appends one dated line. The loop reads this file first: if it
records that the mission is complete, future firings no-op until the hard-stop date.

## Guardrails the loop runs under
- **Branch/PR:** works only on `main-vzvo95`, the single open PR #5. Never merges.
- **Hard stop:** does nothing on or after **2026-08-24**.
- **Cadence:** every 4 hours.
- **Scope:** low-risk, statically-verifiable work only — EditMode unit tests, guardrail
  checkers, dead-code removal, docs, and small single-file wiring that ships with a test.
- **Forbidden:** the `noEngineReferences`/`TrackSpline` rewrite, any assembly-definition or
  cross-assembly change, any multi-file refactor, anything needing a device or real art, and
  stacking changes on a possibly-broken build. When unsure, it does nothing.
- **Verification:** the seven `Tools/CompileCheck/check-*.py` checkers must pass before any
  push; CI (EditMode gate + Android build) runs automatically on every push to the PR.

## Entries
- 2026-08-10 — Loop bounded and scoped (this session). Baseline: PR #5 green on `e403945`
  (EditMode + Android build success). Remaining autonomous candidates: wire `_eventId`,
  `TrackProgress.SetSpline`, `TrackCatalog` into production, and a unit-tested racing-line
  offset in `RivalAIController`; a scene-validation checker for unassigned `[SerializeField]`
  refs; more EditMode tests for Core. Human-only remainder: on-device physics/verification,
  CC0 art + icons, permanent package-ID confirmation, keystore.
