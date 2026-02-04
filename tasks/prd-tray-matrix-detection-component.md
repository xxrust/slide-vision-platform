# PRD: Tray Matrix Detection Component

## Introduction/Overview

当前平台的检测流程以单样本为主，但现场需要支持工件以托盘（矩阵）排列的检测。历史上已有两个实现：
- Python 版本：稳定运行但功能较弱（基础托盘网格、简单看板与手动复测）。
- C# 版本：功能更强、界面更美观（托盘系统管理、旋转、统计、历史、图标/颜色状态、批次/完成逻辑等）。

本 PRD 旨在整合两版能力，形成一个**更全面且可复用的标准化 Tray 检测组件**，并在本项目“帮助菜单”中提供组件说明、接口定义与示例集成方式，供不同算法流程直接调用。

## Goals

- 提供统一的托盘矩阵检测组件（Tray Component），支持多行多列布局、蛇形扫描坐标映射与结果聚合。
- 提供可视化托盘看板：实时更新、历史托盘浏览、NG 过滤/查看。
- 提供标准化接口（API/事件/数据结构）供算法结果接入，不依赖具体算法实现。
- 将组件说明、接口定义、示例集成方式沉淀到“帮助菜单”。

## User Stories

### US-001: 定义托盘布局与坐标映射
**Description:** 作为算法/系统集成方，我希望通过配置定义托盘布局与坐标映射规则，以便算法结果可以准确映射到托盘位置。

**Acceptance Criteria:**
- [ ] 支持配置：行数、列数、起点（左下为 1_1）、行列间距（用于 UI 布局），以及蛇形/顺序映射方式。
- [ ] 提供 `Index -> (Row, Col)` 与 `(Row, Col) -> Index` 的双向映射规则，默认与旧版本一致：奇数行左到右、偶数行右到左。
- [ ] 具备默认布局（例如 10x9），当配置缺失时仍可工作。
- [ ] Typecheck/lint passes.

### US-002: 标准化 Tray 组件接口（API/事件）
**Description:** 作为平台开发者，我需要一个稳定的 Tray 组件接口，以便把算法结果、图像路径、时间戳等传入组件，并接收组件事件。

**Acceptance Criteria:**
- [ ] 提供标准数据结构：`TrayLayout`、`TrayResult`、`TrayStatistics`、`TrayHistoryItem`。
- [ ] 提供核心 API：`StartTray(rows, cols, batchName)`、`UpdateResult(position, result, imagePath, time)`、`CompleteTray()`、`ResetCurrentTray()`、`GetStatistics()`、`GetHistory(limit)`。
- [ ] 支持事件回调：`OnResultProcessed`、`OnTrayCompleted`、`OnError`。
- [ ] 事件回调必须包含 position、result、time、imagePath（如有）。
- [ ] Typecheck/lint passes.

### US-003: 托盘数据与历史管理
**Description:** 作为现场操作人员或工程师，我需要在系统中查看当前托盘与历史托盘状态，以便追溯检测结果。

**Acceptance Criteria:**
- [ ] 当前托盘记录每个位置的结果、图像路径和时间戳。
- [ ] 支持托盘完成逻辑：当检测数量达到总格数时自动完成并转入历史。
- [ ] 历史托盘按最新优先排序，支持分页或最近 N 盘列表。
- [ ] 提供统计信息：总检测数、OK/NG 数量、良率、缺陷分布。
- [ ] Typecheck/lint passes.

### US-004: 实时托盘看板（可视化）
**Description:** 作为操作员，我希望在界面上看到托盘矩阵的实时状态，并能直观区分缺陷类型。

**Acceptance Criteria:**
- [ ] UI 显示带行列坐标的矩阵网格，左下为 (1,1)。
- [ ] 支持状态图标与颜色映射（当前只有良品，异物两类）。
- [ ] 支持“良品隐藏/显示”开关。
- [ ] 支持 90° 旋转显示（不改变逻辑坐标）。
- [ ] Typecheck/lint passes.
- [ ] Verify in browser using dev-browser skill.

### US-005: 单点图像查看与 NG 浏览
**Description:** 作为工程师，我希望点击托盘格子查看对应图像，并快速浏览当前/历史 NG。

**Acceptance Criteria:**
- [ ] 点击格子弹出图像查看窗，展示位置、结果与图像（含缩放适配）。
- [ ] 提供 NG 浏览模式：当前托盘 NG、历史选中托盘 NG、全部历史 NG。
- [ ] NG 浏览支持分页显示与图像路径提示。
- [ ] Typecheck/lint passes.
- [ ] Verify in browser using dev-browser skill.

### US-006: 帮助菜单中的组件文档与示例
**Description:** 作为平台用户/二次开发者，我希望在“帮助菜单”里找到该 Tray 组件的标准化说明与集成示例。

**Acceptance Criteria:**
- [ ] 帮助菜单新增“Tray 检测组件”文档入口。
- [ ] 文档包含：接口说明、数据结构、坐标映射规则、示例代码、缺陷状态定义。
- [ ] 提供最小可运行的集成示例流程（伪代码或真实代码片段）。
- [ ] Typecheck/lint passes.
- [ ] Verify in browser using dev-browser skill.

### US-007: 手动复测与结果刷新
**Description:** 作为操作员，我希望在需要时手动复测某个位置，并自动刷新该位置结果与图像。

**Acceptance Criteria:**
- [ ] 支持对指定位置发起“手动复测”事件回调（由PLC底层系统决定触发 PLC/相机）。
- [ ] 复测完成后更新该位置结果与图像，若图像查看窗打开则自动刷新。
- [ ] Typecheck/lint passes.

## Functional Requirements

- FR-1: 支持托盘布局配置：行数、列数、行间距、列间距、原点（左下 1_1）。
- FR-2: 支持蛇形（S 型）和顺序（逐行）两种坐标映射方式。
- FR-3: 支持结果输入格式 `position = "row_col"`，并兼容 `index` 转换。
- FR-4: 提供托盘状态管理：设置/更新/清空状态与图像路径。
- FR-5: 托盘完成自动判定并触发 `OnTrayCompleted` 事件。
- FR-6: 支持历史托盘管理与分页浏览。
- FR-7: 托盘 UI 显示行列标签与位置一致性（左下为 1_1）。
- FR-8: 缺陷状态支持图标 + 颜色双通道显示，并允许扩展新状态。
- FR-9: 支持良品隐藏/显示切换。
- FR-10: 支持 NG 图像浏览（当前/历史/全部）。
- FR-11: 支持点击单格查看图像与结果信息。
- FR-12: 标准接口与示例文档在“帮助菜单”可访问。

## Non-Goals (Out of Scope)

- 不包含 PLC 通讯、设备控制、报警/告警规则配置。
- 不包含算法训练、模型管理或算法参数调优流程。
- 不负责图像采集与相机驱动控制，仅接收结果与图像路径。

## Design Considerations

- 复用 C# 版托盘 UI 设计（图标+颜色、良品隐藏、旋转显示）。
- 坐标规则与 Python 版一致（蛇形映射），确保历史数据兼容。
- 托盘 UI 以 Grid/矩阵控件实现，避免手动坐标布局。

## Technical Considerations

- 组件需与平台架构解耦，提供纯数据接口与可选 UI 绑定层。
- 需要定义统一缺陷枚举与显示资源（图标/颜色）映射表；缺陷图标放在单独文件夹中进行配置管理。
- 历史托盘数据持久化使用 SQLite（由平台统一存储层实现），Tray 组件仅提供读写接口。

## Success Metrics

- 可在 10 分钟内完成一个新托盘布局配置并开始检测。
- 托盘结果刷新延迟 < 200ms（从算法输出到看板更新）。
- 支持至少 10 个历史托盘快速浏览（翻页响应 < 1s）。

## Open Questions

- Tray 布局配置是否需要支持多套模板（快速切换）？
