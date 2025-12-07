using System.Windows;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Services;

namespace StarResonanceDpsAnalysis.WPF.Helpers;

public static class MouseThroughHelper
{
    /// <summary>
    /// Apply mouse-through state to a window (no-op if window is null).
    /// </summary>
    public static void Apply(Window? window, bool enable, IMousePenetrationService mousePenetrationService)
    {
        if (window == null) return;
        mousePenetrationService.SetMousePenetrate(window, enable);
    }

    /// <summary>
    /// Apply mouse-through state from config to key windows.
    /// </summary>
    public static void ApplyToCoreWindows(AppConfig config, IWindowManagementService windowManager, IMousePenetrationService mousePenetrationService)
    {
        Apply(windowManager.DpsStatisticsView, config.MouseThroughEnabled, mousePenetrationService);
        Apply(windowManager.PersonalDpsView, config.MouseThroughEnabled, mousePenetrationService);
    }
}
