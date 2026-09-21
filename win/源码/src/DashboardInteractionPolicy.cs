using System.Drawing;

namespace CodexMeter
{
    internal enum DashboardAction
    {
        None,
        Sync,
        Menu,
        ResetHistory,
        ToggleDetails
    }

    internal enum DashboardCursorKind
    {
        Default,
        Hand,
        Help
    }

    internal static class DashboardInteractionPolicy
    {
        internal static DashboardAction PrimaryActionAt(Point point,
            Rectangle syncBounds, Rectangle menuBounds, Rectangle resetHistoryBounds,
            Rectangle detailsBounds)
        {
            if (syncBounds.Contains(point))
                return DashboardAction.Sync;
            if (menuBounds.Contains(point))
                return DashboardAction.Menu;
            if (resetHistoryBounds.Contains(point))
                return DashboardAction.ResetHistory;
            if (detailsBounds.Contains(point))
                return DashboardAction.ToggleDetails;
            return DashboardAction.None;
        }

        internal static bool BlocksDrag(DashboardAction action)
        {
            return action != DashboardAction.None;
        }

        internal static DashboardCursorKind CursorFor(DashboardAction action,
            bool budgetMarkerHovered, bool contextualHelpAvailable)
        {
            if (action != DashboardAction.None)
                return DashboardCursorKind.Hand;
            if (budgetMarkerHovered || contextualHelpAvailable)
                return DashboardCursorKind.Help;
            return DashboardCursorKind.Default;
        }
    }
}
