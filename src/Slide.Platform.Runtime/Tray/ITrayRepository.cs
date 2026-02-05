using System;
using System.Collections.Generic;

namespace Slide.Platform.Runtime.Tray
{
    public interface ITrayRepository
    {
        void SaveTrayHeader(TrayData tray);
        void UpdateTrayCompletion(string trayId, DateTime completedAt);
        void SaveMaterial(string trayId, MaterialData material);
        IReadOnlyList<TrayData> LoadRecentTrays(int limit);
    }
}
