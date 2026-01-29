using System;
using GlueInspect.ThreeD.Contracts;

namespace WpfApp2.ThreeD
{
    public sealed class NullThreeDService : IThreeDService
    {
        private readonly ThreeDStatus _status;

        public NullThreeDService(ThreeDStatus status)
        {
            _status = status ?? new ThreeDStatus
            {
                Status = ThreeDAvailabilityStatus.Disabled,
                Message = "3D disabled.",
                HostVersion = "n/a"
            };
        }

        public ThreeDStatus GetStatus(int timeoutMs = 3000) => _status;

        public ThreeDExecuteResult ExecuteLocalImages(ThreeDExecuteLocalImagesRequest request, int timeoutMs = 30000)
        {
            return new ThreeDExecuteResult
            {
                Success = false,
                ErrorMessage = _status?.Message ?? "3D disabled.",
                IsJudgeAllOK = false,
                ExecuteTimeMs = 0
            };
        }

        public bool SaveAfterJudgement(ThreeDSaveAfterJudgementRequest request, out string errorMessage, int timeoutMs = 30000)
        {
            errorMessage = _status?.Message ?? "3D disabled.";
            return false;
        }

        public void Dispose() { }
    }
}
