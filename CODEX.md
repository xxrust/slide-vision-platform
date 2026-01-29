你是运行在本仓库 Platform/ 目录下的自动化代理。
任务：将 PRD 分解为 JSON 结构并写入 prd.json。
输入：Docs/PRD_PlatformHost_WPF48.md。
输出：Platform/prd.json（覆盖即可）。
要求：
- 仅修改 prd.json，不修改其他文件。
- JSON 需包含：title, goal, scope, non_goals, principles, decoupling_scope, reusable_assets, pages, data_persistence, acceptance, milestones, key_paths。
- key_paths 中必须包含：原点胶项目路径、平台工程路径、已验证插件 DLL 路径、已验证示例资源路径。
完成后输出 <promise>COMPLETE</promise>。
