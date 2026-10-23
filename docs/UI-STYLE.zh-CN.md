# 界面验收约定

[English](UI-STYLE.md)

- 英文标题、标签、选项与按钮的每个词均使用首字母大写，例如 `Save Encrypted`、`Topped Up`、`5-Hour`。保留品牌官方拼写、缩写、单位、端点、用户自定义名称及持久化／协议标识符。说明句不按标题处理，不能为了大小写改写源数据。
- 紧凑读数使用短标题：桌面余额行只显示 `DeepSeek`，卡片标题为“余额”。详细说明放在提示信息中。
- 本地推理使用稳定的 `localModelSpeed` 显隐／排序标识，默认位于额度行之后，图标来自后端目录，单位保留小写 `tok/s`。Strata 保留原 SVG 三层图形和既有主题／配色优先级。只有当前生成速度可进入数值单元格；预填充、空闲、未加载、不可用和过期状态不得显示成零或沿用上次实时值。历史速度须标记“上次请求”，不能归于当前模型。只显示本地推理的主界面不能被判为空白。
- DeepSeek 币种显示提供 CNY（RMB）、USD 和全部选项，保存选择并同时应用于主卡片和桌面。只筛选原始余额，不换汇、不编造缺失金额，也不能用另一币种的非零余额替代所选币种的零余额。
- 新增 AI／本地推理主卡片必须复用既有半透明底色、内边距、圆角、标题／图标比例和响应式列宽，不能另接全宽横幅。接入标准拖柄、键盘排序、取消、位置锁定与排序持久化；轮询不能在拖动期间重建卡片。非数值状态保持紧凑，遥测长说明放入提示信息。必须与已有卡片同屏验收，不能仅凭孤立的单卡截图放行。
- DeepSeek 桌面显隐与排序共用稳定的 `quotaDeepSeek` 分组，其所有币种和错误／状态行均跟随该分组。切换币种不能改变位置或让已隐藏的行重新出现。桌面隐藏不影响监控查询或主卡片。
- 必须实际检查英语、简体中文与明暗主题下的 WPF 设置、卡片和窄桌面读数。使用合成数据运行 `scripts/Test-QuotaFeature.ps1 -AppPath <isolated-built-app>`。编译成功或只截主窗口不能证明桌面行布局通过验收。
- Argon 是原 Antigravity 来源下的独立周额度，不新增供应商或登录。桌面使用 `Argon · Weekly`，应用内使用 Argon 子区块。保留独立重置时间和真实零值，不借用 Gemini 五小时窗口、不合并百分比。窗口使用稳定标识保存显隐／排序；升级不能让原来完全隐藏的 Gemini 分组重新出现。
- Manual Refresh 沿用 0.6.65 的 explicit forced request 行为。仅在 request in flight 时禁用，重复 click 不进入 queue。Automatic polling 保留 cadence、Retry-After 和 failure backoff；不要恢复旧版 cooldown-disabled manual button。通过 `scripts/Test-Core.ps1` 和 native quota feedback tests 验证两条路径。
- Settings 无法读取 profile 时必须保留原文件，不能用 defaults 覆盖；显示 pending/saved/failed status，diagnostic IO failure 不得中断 UI event loop。Settings categories 支持 Left/Right/Home/End，slider 在两个 theme 中均显示 keyboard focus。
- 共享冲突回退只能使用原有十五秒有效期内的已验证样本，保留原时间，并在应用及桌面明确显示“最近读数／正在重读快照”。缺失、拒绝访问、畸形或过期快照不能维持旧的实时值。区分快照读取失败和未经证实的采集器退出。
