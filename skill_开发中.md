# Skill Draft (DO NOT ACTIVATE)

Name: GlueInspect Platform Industrial Project Integrator (Draft)
Status: draft
Filename: skill_开发中.md
Scope: PlatformHost.Wpf + Platform plugin framework integration

## Purpose
Provide a repeatable workflow to adapt the GlueInspect Platform framework to an industrial project by defining required inputs, mapping parameters, integrating algorithm engines/plugins, and wiring IO/PLC outputs.

## Trigger (manual only)
Use only when the user explicitly requests industrial project integration for this framework and asks to produce or update a skill.

## Required Inputs
- Project meta: sample types, coating types, step list, defect taxonomy
- Algorithm route: Plugin (Platform/) or WPF engine (PlatformHost.Wpf/Algorithms)
- Parameter spec: names, units, ranges, defaults, conversion rules
- Output spec: metric names, limits, OK/NG logic, defect naming
- Hardware: IO/PLC model, mapping, timing, and deploy requirements
- Acceptance: performance constraints, validation criteria

## Expected Outputs
- Updated engine/plugin mapping
- Updated step/parameter registry and templates
- Updated IO/PLC mapping and runtime handling
- Documentation of validation steps and required assets

## Key Paths (project-specific)
- Base project: `E:\posen_project\点胶检测\上位机程序\WpfApp2`
- Platform root: `E:\posen_project\点胶检测\上位机程序\WpfApp2\Platform`
- Abstractions: `src/GlueInspect.Platform.Abstractions/`
- Runtime loader: `src/GlueInspect.Platform.Runtime/`
- WPF host: `PlatformHost.Wpf/`
- Algorithms (WPF): `PlatformHost.Wpf/Algorithms/`
- UI flow: `PlatformHost.Wpf/UI/`
- Templates: `PlatformHost.Wpf/Templates/` (created at runtime)
- Configs: `PlatformHost.Wpf/Config/`
- Parameter images: `PlatformHost.Wpf/Resources/ParameterImages/`
- IO/PLC: `PlatformHost.Wpf/SMTGPIO/`

## Core Data Flow
1) UI/template -> AlgorithmInput (Page1.BuildAlgorithmInput + PopulateAlgorithmInputParameters)
2) Engine/Plugin Execute -> AlgorithmResult
3) Normalize/Apply results to UI cache and statistics
4) Optional IO/PLC output after detection

## Mandatory Touchpoints (functions/files)

### Engine selection and registration
- `PlatformHost.Wpf/Algorithms/AlgorithmEngineRegistry.cs`
  - Initialize() registers engines
  - GetDefaultDescription() defines UI hints
- `PlatformHost.Wpf/UI/Models/AlgorithmEngineSettings.cs`
  - PreferredEngineId + AlgorithmEngine.json

### Algorithm input mapping
- `PlatformHost.Wpf/UI/Page1.xaml.cs`
  - BuildAlgorithmInput(...)
  - PopulateAlgorithmInputParameters(...)

### Algorithm result mapping
- `PlatformHost.Wpf/UI/Page1.xaml.cs`
  - ExecuteAlgorithmEngineDetectionAsync(...)
  - NormalizeAlgorithmResult(...)
  - ApplyAlgorithmResultTo2DCache(...)
  - BuildAlgorithmResult(...)

### Step/parameter registry
- `PlatformHost.Wpf/UI/Models/ModuleRegistry.cs`
  - RegisterAllDefaultModules() defines steps and parameters
- `PlatformHost.Wpf/UI/Models/ModuleDefinition.cs`
  - Parameter conversion and mapping
- `PlatformHost.Wpf/UI/Models/Class1.cs`
  - StepType / SampleType / CoatingType / TemplateParameters

### IO/PLC
- `PlatformHost.Wpf/SMTGPIO/IOManager.cs`
  - Initialize(), SetDetectionResult(...)
- `PlatformHost.Wpf/SMTGPIO/PLCSerialController.cs`
  - PLC communication specifics
- `PlatformHost.Wpf/App.xaml.cs`
  - Startup/exit initialization and teardown

### Plugin path (optional)
- `src/GlueInspect.Platform.Abstractions/IAlgorithmPlugin.cs`
- `src/GlueInspect.Platform.Abstractions/IAlgorithmSession.cs`
- `src/GlueInspect.Platform.Runtime/PluginLoader.cs`
- `src/GlueInspect.Platform.Runtime/AlgorithmRegistry.cs`

## Deliverables Checklist
- [ ] New/updated SampleType + CoatingType + StepType entries
- [ ] ModuleRegistry mappings for each step (inputs/outputs/actions)
- [ ] Template JSON files for each production template
- [ ] Algorithm engine or plugin implementation + registration
- [ ] Output metrics mapped to UI/analysis expectations
- [ ] IO/PLC mapping updated and validated
- [ ] Release build + manual verification

## Questions to Ask the User
1) Which route: WPF engine or Platform plugin (or both)?
2) Full step list and parameter definitions?
3) Output metrics and OK/NG rules?
4) Hardware model and IO/PLC mapping?
5) Performance constraints and acceptance tests?
6) Who owns template JSON generation/maintenance?

## Non-Goals
- Do not redesign UI
- Do not add new business pages
- Do not alter unrelated VM logic unless required

## Validation Steps (manual)
- Build Release and run GlueInspect.exe
- Verify TemplateConfigPage + Page1 flow
- Confirm AlgorithmEngine.json selection
- Validate ParameterConfigs.json descriptions and images
- Confirm IO/PLC signal behavior on hardware

## Notes
- Keep Chinese annotations in existing files intact.
- Store large assets in contents/ or ImageTemp/.
- Avoid changing unrelated files or UI flows.
