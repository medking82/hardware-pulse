# Product check: reliability of displayed state

Mode: prototype maturity, using the requested ai-product-audit workflow. Pulse is
not an AI runtime product; prompt/retrieval/agent modes are not applicable here.
User: a Windows desktop/laptop owner installing a small read-only status widget.
Job: recognize the machine's readings at a glance and understand failures/updates.
Current stage: stabilize repeated install, monitoring and update workflows across
different PCs. Success in this pass means specific state transitions are correct
and understandable, with a failing reproduction followed by passing checks.

| Priority | Evidence | Consequence | Change and validation |
| --- | --- | --- | --- |
| 1 | A headless stale-snapshot test lost the previously discovered CPU name; WPF consumed the fallback label | Delayed data looks like lost hardware discovery | Retain device labels alongside capabilities, keep live values empty, replace labels on recovery; headless and WPF tests pass |
| 2 | A real WPF test completed update checking, changed Chinese to English, and retained the Chinese result | The selected language does not govern the whole current screen | Refresh dynamic monitor/updater presentation on language changes; WPF regression and localized Settings capture pass |
| Deferred | Settings capture still uses a light native language ComboBox inside the dark material | Visual styling is inconsistent | A future control-template change needs keyboard, open-popup and contrast checks; no template was changed in this pass |
| Unknown | Earlier friend reports mention security software and missing laptop fan readings | Installation or hardware support may still vary by PC | Obtain the exact security event and current snapshot before attributing causes or adding device support |

Diagnosis after failing reproductions:

- Stale-name hypotheses: state parser discards names (confirmed at ReadingSession);
  malformed fixture/device metadata (rejected by the preceding valid live read);
  translation or layout hiding text (rejected by the headless failure).
- Language hypotheses: dynamic status omitted from refresh (confirmed in Localize);
  missing catalog translation (rejected by successful initial translated check);
  late check overwrites new language (rejected by switching only after completion).

Apple-design acceptance applied here: preserve recognizable identity, give current
and language-consistent feedback, and retain the existing focus/size/material
behavior. The native WPF capture was inspected; 10/12/16 DIP, narrow-header and
material fallback regressions passed. This is not a complete keyboard/screen-reader
or all-wallpapers contrast audit.

Non-goals: new AI features, universal fan support claims, new glass effects, a
whole-app redesign, or another architecture rewrite. Next decision: prioritize
new reproducible friend reports versus the bounded language-picker visual polish;
neither is claimed complete by these two fixes.
