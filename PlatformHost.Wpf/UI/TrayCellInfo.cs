using System;

namespace WpfApp2.UI
{
    public sealed class TrayCellInfo
    {
        public TrayCellInfo(int row, int col, string result, string imagePath, DateTime detectionTime)
        {
            Row = row;
            Col = col;
            Result = result;
            ImagePath = imagePath;
            DetectionTime = detectionTime;
        }

        public int Row { get; }
        public int Col { get; }
        public string Result { get; }
        public string ImagePath { get; }
        public DateTime DetectionTime { get; }
    }
}
