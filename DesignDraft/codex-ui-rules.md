# codex-ui-rules.md

# XFVerse Codex UI Rebuild Rules

本文件用于约束 Codex 在 XFVerse UI 重构中的执行方式。

---

## 1. Non-Negotiable Rules

Codex 必须遵守：

- 不修改后端业务逻辑
- 不擅自删除功能
- 不擅自改变无边记页面布局
- 不擅自改变滚动区域
- 不擅自改变组件顺序
- 不重新设计页面结构
- 不用自己的审美替换既定设计方向
- 不在页面中硬编码颜色
- 不混用多套图标风格
- 每次修改后必须保证项目可编译

只允许：

- 修正明显不合理的间距
- 修正明显不合理的对齐
- 优化实现方式
- 抽取公共组件
- 统一样式资源
- 按 DESIGN.md 改善视觉一致性

---

## 2. Project Constraints

项目技术栈：

- WPF + .NET
- WPF 内嵌 mpv 播放器
- 后端功能已完成
- 旧 UI 很简陋
- 新 UI 需要新建一套 Views / Controls，逐步替换旧 UI
- UI 必须支持浅色 / 深色双主题
- 基础 UI 库使用 WPF UI

---

## 3. Source of Truth

Codex 实现 UI 时，按以下优先级理解需求：

1. 当前任务 Prompt
2. `UI-REBUILD-README.md`
3. 对应页面 md
4. `DESIGN.md`
5. `resources-note.md`
6. `codex-ui-rules.md`
7. 当前项目真实代码结构
8. 截图

说明：

- 截图只作为布局辅助参考，不作为颜色、样式和业务规则来源。
- 如果截图和 md 冲突，以 md 为准。
- 如果 md 和现有业务逻辑冲突，不要直接改业务逻辑，先记录冲突并提出最小处理建议。
---

## 4. WPF UI Rules

- 使用 WPF UI 作为基础 UI 库
- 基础按钮、输入框、下拉框、菜单、弹窗、导航、滚动条优先使用 WPF UI 默认样式
- 不从零重写基础控件样式
- WPF UI 默认图标优先
- 不足部分再考虑 Fluent / IconPack
- 不混用风格差异明显的图标库

---

## 5. ResourceDictionary Rules

所有视觉资源必须统一管理。

应建立或使用类似结构：

- Colors.Light.xaml
- Colors.Dark.xaml
- Typography.xaml
- Buttons.xaml
- Inputs.xaml
- Cards.xaml
- Tags.xaml
- Lists.xaml
- Dialogs.xaml
- Player.xaml

要求：

- 页面内不要硬编码颜色
- 页面内不要重复写大段样式
- 业务状态颜色必须复用统一资源
- 深色 / 浅色主题必须通过资源切换实现
- 组件样式应尽量复用 StaticResource / DynamicResource

---

## 6. Componentization Rules

新 UI 应尽量组件化。

优先抽取：

- 影片卡片
- 播放源卡片
- 标签控件
- 状态标签
- 评分卡
- 设置项控件
- EmptyState
- 识别修正结果卡片
- 播放器控制栏
- 播放器音量 / 亮度浮层
- 观影洞察图表组件
- 用户画像卡片组件

页面应该负责组合组件，不应把所有 UI 写在一个巨大 XAML 文件里。

---

## 7. Page-Spec Rules

每个页面重构前必须参考对应 page-spec.md。

page-spec.md 应说明：

- 页面用途
- 布局结构
- 滚动区域
- 组件拆分
- 数据字段
- 按钮与交互
- 状态处理
- 主题注意事项
- 验收标准

Codex 不应在缺少 page-spec 的情况下直接重构复杂页面。

---

## 8. Business Logic Rules

Codex 不得破坏现有业务逻辑。

要求：

- 保留现有 ViewModel / Service / Command
- 优先复用已有绑定
- 不擅自改数据库结构
- 不擅自改 WebDAV 逻辑
- 不擅自改 mpv 播放逻辑
- 不擅自改 AI 推荐逻辑
- 不擅自改扫描识别逻辑

如果发现旧 UI 绑定不适合新 UI：

- 先说明问题
- 给出最小改动方案
- 不直接大改后端

---

## 9. Build and Verification

每个阶段完成后必须验证：

- 项目可以编译
- 页面可以打开
- 关键绑定没有丢失
- 按钮命令仍然存在
- 深色 / 浅色主题可用
- 主要滚动区域可用
- 没有明显布局溢出

如果无法运行完整应用，至少执行：

- dotnet build

并汇报结果。

---

## 10. What Codex Must Report

每次完成一个 UI 重构任务后，Codex 需要输出：

- 修改了哪些文件
- 新增了哪些文件
- 完成了哪些目标
- 哪些功能未做
- 是否修改了后端逻辑
- build / 验证结果
- 已知问题
- 是否建议进入下一步

输出格式需要清晰，方便人工验收。

---

## 11. Do Not Do

Codex 不要做：

- 不要擅自重排页面
- 不要删掉用户设计好的功能
- 不要把控件做成图片
- 不要用 AI 生成图替代真实 UI 控件
- 不要把图表做成静态图片
- 不要把按钮、输入框、卡片做成 PNG
- 不要把所有内容塞进单个页面 XAML
- 不要混用多个 UI 风格
- 不要为了好看牺牲可用性
- 不要绕过 MVVM 直接写大量 code-behind 逻辑

---

## 12. UI Rebuild Strategy

重构策略：

- 新建一套 Views / Controls
- 先建立主题资源和公共组件
- 再按页面逐步替换旧 UI
- 每个页面完成后单独验收
- 最后做全局统一和细节修复

推荐施工顺序由后续 UI 重构计划决定。