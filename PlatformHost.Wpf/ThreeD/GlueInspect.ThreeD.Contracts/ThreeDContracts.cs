using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GlueInspect.ThreeD.Contracts
{
    // NOTE: This assembly must remain free of any Keyence/LjDev/LjdSampleWrapper references.

    [DataContract]
    public enum ThreeDAvailabilityStatus
    {
        [EnumMember] Disabled = 0,
        [EnumMember] NotInstalled = 1,
        [EnumMember] NotAuthorized = 2,
        [EnumMember] Available = 3,
        [EnumMember] Error = 4
    }

    [DataContract]
    public sealed class ThreeDStatus
    {
        [DataMember(Order = 1)] public ThreeDAvailabilityStatus Status { get; set; }
        [DataMember(Order = 2)] public string Message { get; set; }
        [DataMember(Order = 3)] public string HostVersion { get; set; }
    }

    [DataContract]
    public sealed class ThreeDConfig
    {
        [DataMember(Order = 1)] public bool Enable3DDetection { get; set; }
        [DataMember(Order = 2)] public string ProjectName { get; set; }
        [DataMember(Order = 3)] public string ProjectFolder { get; set; }
        [DataMember(Order = 4)] public bool ReCompile { get; set; }
    }

    [DataContract]
    public sealed class ThreeDExecuteLocalImagesRequest
    {
        [DataMember(Order = 1)] public ThreeDConfig Config { get; set; }
        [DataMember(Order = 2)] public string HeightImagePath { get; set; }
        [DataMember(Order = 3)] public string GrayImagePath { get; set; }
    }

    [DataContract]
    public sealed class ThreeDSaveAfterJudgementRequest
    {
        [DataMember(Order = 1)] public string DefectType { get; set; }
        [DataMember(Order = 2)] public int ImageNumber { get; set; }
        [DataMember(Order = 3)] public string RootDirectory { get; set; }
        [DataMember(Order = 4)] public bool SaveAllImages { get; set; }
    }

    [DataContract]
    public sealed class ThreeDDetectionItem
    {
        [DataMember(Order = 1)] public string Name { get; set; }
        [DataMember(Order = 2)] public string ValueString { get; set; }
        [DataMember(Order = 3)] public bool IsOutOfRange { get; set; }
        [DataMember(Order = 4)] public int ToolIndex { get; set; }
    }

    [DataContract]
    public sealed class ThreeDExecuteResult
    {
        [DataMember(Order = 1)] public bool Success { get; set; }
        [DataMember(Order = 2)] public string ErrorMessage { get; set; }
        [DataMember(Order = 3)] public bool IsJudgeAllOK { get; set; }
        [DataMember(Order = 4)] public double ExecuteTimeMs { get; set; }
        [DataMember(Order = 5)] public List<ThreeDDetectionItem> Items { get; set; } = new List<ThreeDDetectionItem>();
        [DataMember(Order = 6)] public Dictionary<string, string> Extra { get; set; } = new Dictionary<string, string>();
    }

    [DataContract]
    public sealed class ThreeDIpcRequest
    {
        [DataMember(Order = 1)] public Guid RequestId { get; set; } = Guid.NewGuid();
        [DataMember(Order = 2)] public string Command { get; set; }

        // Optional payloads (depending on Command)
        [DataMember(Order = 3)] public ThreeDConfig Config { get; set; }
        [DataMember(Order = 4)] public ThreeDExecuteLocalImagesRequest ExecuteLocalImages { get; set; }
        [DataMember(Order = 5)] public ThreeDSaveAfterJudgementRequest SaveAfterJudgement { get; set; }
    }

    [DataContract]
    public sealed class ThreeDIpcResponse
    {
        [DataMember(Order = 1)] public Guid RequestId { get; set; }
        [DataMember(Order = 2)] public bool Success { get; set; }
        [DataMember(Order = 3)] public string ErrorMessage { get; set; }

        [DataMember(Order = 4)] public ThreeDStatus Status { get; set; }
        [DataMember(Order = 5)] public ThreeDExecuteResult ExecuteResult { get; set; }
    }
}

