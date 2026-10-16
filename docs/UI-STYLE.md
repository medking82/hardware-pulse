# UI Acceptance

[简体中文](UI-STYLE.zh-CN.md)

- Use Title Case for every word in English headings, labels, choices and buttons, including `Save Encrypted`, `Topped Up` and `5-Hour`. Preserve official brand spelling, acronyms, units, endpoints, user-defined names and persisted/protocol identifiers. Explanatory sentences are not captions; never change source data for capitalization.
- Keep compact readings concise: the desktop balance title is `DeepSeek`, and the card caption is `Balance`. Put the detailed description in the tooltip.
- DeepSeek currency display is a persisted CNY (RMB), USD or All choice shared by App and Desktop. Filter original balances without conversion, fabricated amounts or substitution of another currency for a selected zero balance.
- DeepSeek desktop visibility and order use the stable `quotaDeepSeek` group. All its currency and error/status rows follow that group. Currency changes must not move the group or expose hidden rows. Desktop visibility does not disable monitoring or the App card.
- Verify actual WPF settings, cards and narrow desktop readings in English and Simplified Chinese, in light and dark themes. Run `scripts/Test-QuotaFeature.ps1 -AppPath <isolated-built-app>` with synthetic fixtures. Compilation or a main-window screenshot alone does not verify desktop-row layout.
