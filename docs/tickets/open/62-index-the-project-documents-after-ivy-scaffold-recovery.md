---
format: 3
status: idea
created: 2026-10-02T03:17:15Z
origin: system-detected
tags: ["rounds", "documentation", "onboarding"]
value: 3
risk: 2
sessions:
  - codex:01a0fa96-a468-7332-92f6-c36c3908e4b9
execution: unattended
parent: 59
depends-on: []
supersedes: []
split-from: []
---

# Index the project documents after Ivy scaffold recovery

The current Ivy onboarding helper created missing ivy.yaml, docs/postmortems.md, docs/design-docs/index.html and theme.css in the integration root. Preserve the existing human edits and classify the project documents before any conversion so the restored index can link useful records without disturbing the game work.

## Outcome

- The documentation index links project-owned records and references with appropriate formats and frame navigation.
- Existing journals, source measurements and README remain readable in their current homes; any conversion is separately admitted against its current reason.

## Decisions

- Keep this maintenance ticket at idea while product slices advance. The scaffold files are uncommitted onboarding outputs; do not silently discard or overwrite them.
- The integration root already had human changes in AGENTS.md and docs/design-docs/postmortems.md plus untracked .claude and CLAUDE.md. Find and preserve their provenance; do not copy unrelated bytes into a candidate.

## Evidence required

- A path inventory accounts for every project document and the onboarding outputs, with one proposed format and destination per path.
- The release-matched documentation checker and git diff --check pass for any delivered index changes; no unrelated root edit is committed.

## Scratch

- `research\notes\maps.md` — source measurement/reference; retain current format
- `research\notes\cards.md` — source measurement/reference; retain current format
- `research\notes\core-rules.md` — source measurement/reference; retain current format
- `docs\tickets\open\052-match-the-source-airborne-horizontal-speed-with-a-general-control-change.md` — journal or ticket; retain Markdown
- `docs\tickets\open\049-reproduce-the-short-hop-at-the-ice-platform-edge.md` — journal or ticket; retain Markdown
- `docs\tickets\open\037-reconcile-ticket-035-delivery-residue.md` — journal or ticket; retain Markdown
- `docs\tickets\open\036-keep-project-failure-postmortems-current.md` — journal or ticket; retain Markdown
- `docs\tickets\open\035-add-unattended-base-projectile-evidence-state.md` — journal or ticket; retain Markdown
- `docs\tickets\open\034-remove-dark-base-projectile-ring.md` — journal or ticket; retain Markdown
- `docs\tickets\open\032-add-non-disruptive-closed-loop-clone-playtesting.md` — journal or ticket; retain Markdown
- `docs\tickets\open\031-establish-non-disruptive-installed-rounds-evidence-capture.md` — journal or ticket; retain Markdown
- `docs\tickets\open\025-expand-rounds-cards-through-verified-groups.md` — journal or ticket; retain Markdown
- `docs\tickets\open\024-restore-nightly-replay-reel-evidence.md` — journal or ticket; retain Markdown
- `docs\tickets\open\023-match-rounds-settings-persistence-and-shipping.md` — journal or ticket; retain Markdown
- `docs\tickets\open\022-add-match-replay-and-headless-self-play.md` — journal or ticket; retain Markdown
- `docs\tickets\open\021-match-rounds-controller-and-menu-input.md` — journal or ticket; retain Markdown
- `docs\tickets\open\020-replace-invented-presentation-with-rounds-fidelity.md` — journal or ticket; retain Markdown
- `docs\tickets\open\019-verify-and-gate-current-rounds-card-mechanics.md` — journal or ticket; retain Markdown
- `docs\tickets\open\018-reconstruct-arenas-from-direct-rounds-evidence.md` — journal or ticket; retain Markdown
- `docs\tickets\open\017-match-rounds-projectile-presentation.md` — journal or ticket; retain Markdown
- `docs\tickets\open\016-match-base-rounds-movement-and-combat-feel.md` — journal or ticket; retain Markdown
- `research\evidence\projectile-speed-ssag-frame-spans.json` — source measurement/reference; retain current format
- `docs\decisions.md` — journal or ticket; retain Markdown
- `docs\architecture.md` — project document; assess as index link or design record before conversion
- `docs\tickets\closed\058-begin-a-new-match-from-waiting.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\057-end-a-match-from-completed-round-scoring.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\055-carry-the-connected-loser-draft-into-the-held-hanging-arena-entry.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\054-correct-the-footage-coverage-ledger.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\053-run-the-first-loser-draft-through-flow-and-presentation.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\051-hold-scripted-jumps-through-their-airborne-ticks.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\050-match-the-source-jump-arc-with-a-general-vertical-control-change.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\047-resolve-simultaneous-elimination-without-post-death-combat.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\046-continue-the-connected-match-through-ice-and-its-first-round-result.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\045-connect-rematch-drafts-and-timber-result-in-one-playable-session.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\044-bound-cargo-resource-use-without-reducing-verification.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\043-reproduce-the-yellow-crate-terminal-blast-and-radial-screen-echo.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\042-reproduce-the-radial-saw-duel-and-half-blue-transition.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\041-reproduce-rematch-and-card-draft-flow.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\040-reproduce-explosive-timber-collapse.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\039-reproduce-teal-static-duel-end-to-end.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\038-archive-legacy-prototype-and-establish-bevy-rewrite.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\033-remove-unsupported-live-ui-terminology.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\030-correct-base-rounds-projectile-speed-from-frame-span-evidence.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\015-restore-faithful-rounds-identity.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\014-recover-ricochet-projectile-cards.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\013-make-orphaned-project-progress-safely-recoverable.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\008-implement-match-loop-and-stat-cards.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\007-add-deterministic-replays-and-reel.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\006-implement-base-combat-duel.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\005-implement-movement-and-static-collision.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\004-catalog-vanilla-maps.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\003-catalog-vanilla-cards.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\002-research-core-rules-and-measurements.md` — journal or ticket; retain Markdown
- `docs\tickets\closed\001-bootstrap-deterministic-skeleton.md` — journal or ticket; retain Markdown
- `docs\design-docs\postmortems.md` — journal or ticket; retain Markdown
- `docs\design-docs\index.html` — project document; assess as index link or design record before conversion
- `docs\fidelity\match-end-waiting-observations.md` — source measurement/reference; retain current format
- `docs\fidelity\ice-round-observations.md` — source measurement/reference; retain current format
- `docs\fidelity\held-hanging-entry-observations.md` — source measurement/reference; retain current format
- `docs\fidelity\footage-coverage.md` — source measurement/reference; retain current format
- `docs\fidelity\first-loser-draft-observations.md` — source measurement/reference; retain current format
- `docs\fidelity\yellow-crate-terminal-blast-observations.md` — source measurement/reference; retain current format
- `docs\fidelity\timber-collapse-observations.md` — source measurement/reference; retain current format
- `docs\fidelity\teal-duel-observations.md` — source measurement/reference; retain current format
- `docs\fidelity\rematch-draft-observations.md` — source measurement/reference; retain current format
- `docs\fidelity\radial-saw-half-observations.md` — source measurement/reference; retain current format
- `docs\fidelity\new-match-draft-observations.md` — source measurement/reference; retain current format
- `docs\postmortems.md` — journal or ticket; retain Markdown
- `docs\legacy-prototype.md` — project document; assess as index link or design record before conversion
- `docs\fidelity\connected-match-observations.md` — source measurement/reference; retain current format
- `docs\recovery\orphaned-progress-2026-08-28.md` — project document; assess as index link or design record before conversion

## Work log

- 2026-10-02T03:17:15Z stage research start session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Inventoried documentation after the release-matched onboarding helper restored missing scaffold files.
- 2026-10-02T03:17:15Z stage research end session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Retained the complete path inventory as an idea ticket and deferred conversions while source-backed product delivery proceeds.