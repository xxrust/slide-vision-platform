using System;
using Slide.ThreeD.Contracts;

namespace WpfApp2.ThreeD
{
    /// <summary>
    /// Main-process abstraction for 3D. Must not reference any Keyence/LjDev/Ljd assemblies.
    /// </summary>
    public interface IThreeDService : IDisposable
    {
        ThreeDStatus GetStatus(int timeoutMs = 3000);

        ThreeDExecuteResult ExecuteLocalImages(ThreeDExecuteLocalImagesRequest request, int timeoutMs = 30000);

        bool SaveAfterJudgement(ThreeDSaveAfterJudgementRequest request, out string errorMessage, int timeoutMs = 30000);
    }
}
