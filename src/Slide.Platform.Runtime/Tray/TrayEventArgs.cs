using System;

namespace Slide.Platform.Runtime.Tray
{
    public class TrayResultEventArgs : EventArgs
    {
        public TrayResultEventArgs(TrayPosition? position, string result, string imagePath, DateTime detectionTime)
        {
            Position = position;
            Result = result;
            ImagePath = imagePath;
            DetectionTime = detectionTime;
        }

        public TrayPosition? Position { get; }
        public string Result { get; }
        public string ImagePath { get; }
        public DateTime DetectionTime { get; }
    }

    public sealed class TrayCompletedEventArgs : TrayResultEventArgs
    {
        public TrayCompletedEventArgs(TrayData tray, TrayResultEventArgs lastResult)
            : base(lastResult?.Position, lastResult?.Result, lastResult?.ImagePath, lastResult?.DetectionTime ?? DateTime.UtcNow)
        {
            Tray = tray;
        }

        public TrayData Tray { get; }
    }

    public sealed class TrayErrorEventArgs : TrayResultEventArgs
    {
        public TrayErrorEventArgs(TrayPosition? position, string result, string imagePath, DateTime detectionTime, Exception error)
            : base(position, result, imagePath, detectionTime)
        {
            Error = error;
        }

        public Exception Error { get; }
    }

    public sealed class TrayRetestEventArgs : EventArgs
    {
        public TrayRetestEventArgs(TrayPosition position)
        {
            Position = position;
        }

        public TrayPosition Position { get; }
    }
}
