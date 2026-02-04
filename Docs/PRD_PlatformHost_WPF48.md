# PRD — 平台与项目去耦（以点胶项目为基座）

## 1. 目标

先完成“点胶项目的平台与项目内容去耦”，再与新建的通用接口（插件协议）耦合。
不新增功能，不重做 UI；只做**抽离与替换**，确保原有完整流程可跑通。

核心目标：
- 复用点胶项目已有 UI / 流程 / 数据分析 / 日志 / 配置。
- 把“项目绑定内容”抽离成通用接口与项目包配置。
- 通过 OpenCV/ONNX 示例插件验证框架完整性。

非目标：
- 不新增低代码式“算法下拉选择”。
- 不新增新的业务功能页面。
- 不改点胶项目本体（平台工程独立）。

---

## 2. 基本原则

- **复用优先**：保留点胶项目已验证流程与界面结构。
- **去耦优先**：先剥离项目绑定内容，再接入统一接口。
- **最少改动**：只改“绑定层”，不新增功能。

---

## 3. 去耦范围（从点胶基座抽离）

### 3.1 项目绑定内容
- 样品类型/步骤/参数（点胶专有）
- VM/算法调用方式
- 结果字段（点胶专有指标名）

### 3.2 抽离后的通用接口
- 算法调用：`IAlgorithmPlugin` / `IAlgorithmSession`
- 输入：`AlgorithmInput`（图像路径/字节 + 参数）
- 输出：`AlgorithmResult`（Success/Message/Metrics/Tags）

### 3.3 项目包（Project Package）
- 作为“项目绑定内容”的唯一载体
- 定义：输入源配置、参数模板、算法绑定（插件ID）、输出指标映射

---

## 4. UI 与流程（复用点胶）

- 保持点胶项目的 UI 页面结构与操作流程
- 图像选择/路径校验逻辑沿用点胶实现
- 数据分析页与图表逻辑沿用点胶实现
- 日志与导出沿用点胶实现

> 这里只替换“算法调用与结果绑定”，不新增新功能页面。

---

## 5. 数据持久化

- 延用点胶项目的数据记录方式
- 增加 Result/Metrics 的通用落库映射（若已有持久化则直接复用）

---

## 6. 示例验证

- OpenCV 插件：跑通输入 → 输出 Metrics
- ONNX 插件：跑通输入 → 输出 Metrics
- 示例项目包：验证“项目包驱动 + 插件绑定 + 数据分析”链路完整

---

## 7. 验收标准

- UI 与流程与点胶项目一致（没有新功能/新页面）
- 算法调用完全走接口（不再直接绑定项目算法）
- 项目绑定内容全部从代码移至项目包
- OpenCV / ONNX 示例可运行并输出 Metrics
- 结果仍可进入分析与统计流程

---

## 8. 实施顺序

1) 复用点胶 UI/流程作为平台基座
2) 抽离项目绑定内容 → 项目包
3) 替换算法调用 → 插件接口
4) 用 OpenCV/ONNX 示例验证完整性

---

> 若 PRD 认可，将使用 Ralph 按步骤迭代。

---

## 9. 重要路径与已验证资源

- 原点胶项目路径（基座）：
  `E:\\posen_project\\点胶检测\\上位机程序\\WpfApp2`

- 平台工程路径（本框架）：
  `E:\\posen_project\\点胶检测\\上位机程序\\WpfApp2\\Platform`

- 已验证插件（示例）：
  - OpenCV 插件 DLL：
    `E:\\posen_project\\点胶检测\\上位机程序\\WpfApp2\\Platform\\plugins\\Slide.Algorithm.OpenCv\\bin\\Debug\\netstandard2.0\\Slide.Algorithm.OpenCv.dll`
  - ONNX 插件 DLL：
    `E:\\posen_project\\点胶检测\\上位机程序\\WpfApp2\\Platform\\plugins\\Slide.Algorithm.Onnx\\bin\\Debug\\netstandard2.0\\Slide.Algorithm.Onnx.dll`

- 已验证插件运行宿主：
  `E:\\posen_project\\点胶检测\\上位机程序\\WpfApp2\\Platform\\samples\\Slide.Platform.SampleHost`

- 已验证示例资源：
  - OpenCV 输入图像：
    `E:\\posen_project\\点胶检测\\上位机程序\\WpfApp2\\Platform\\samples\\assets\\test.pgm`
  - ONNX 模型：
    `E:\\posen_project\\点胶检测\\上位机程序\\WpfApp2\\Platform\\samples\\assets\\identity.onnx`
