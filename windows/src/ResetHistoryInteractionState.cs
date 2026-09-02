using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace CodexMeter
{
    internal sealed class ResetHistoryInteractionState
    {
        internal bool ShowAll { get; private set; }
        internal int ListOffset { get; private set; }
        internal int TimelineStartDay { get; private set; }
        internal int TimelineMaximumStartDay { get; private set; }
        internal int TimelineTotalDays { get; private set; }
        internal bool DraggingTimelineSlider { get; private set; }
        internal float TimelineSliderDragOffset { get; private set; }

        internal ResetHistoryInteractionState()
        {
            TimelineTotalDays = 1;
        }

        internal void Expand(IList<DateTimeOffset> timelineDays, int viewportDays)
        {
            ShowAll = true;
            MoveTimelineToLatest(timelineDays, viewportDays);
        }

        internal void Toggle(IList<DateTimeOffset> timelineDays, int viewportDays)
        {
            ShowAll = !ShowAll;
            if (!ShowAll)
            {
                ListOffset = 0;
                EndTimelineDrag();
                return;
            }
            MoveTimelineToLatest(timelineDays, viewportDays);
        }

        internal bool ScrollList(int totalEntries, int visibleRows, int steps)
        {
            if (ShowAll || steps == 0)
                return false;
            int maximum = Math.Max(0, totalEntries - visibleRows);
            int next = Math.Max(0, Math.Min(maximum, ListOffset + steps));
            if (next == ListOffset)
                return false;
            ListOffset = next;
            return true;
        }

        internal List<T> VisibleEntries<T>(IList<T> entries, int visibleRows)
        {
            if (entries == null)
                return new List<T>();
            if (ShowAll)
                return entries.ToList();

            int maximum = Math.Max(0, entries.Count - visibleRows);
            ListOffset = Math.Max(0, Math.Min(maximum, ListOffset));
            return entries.Skip(ListOffset).Take(visibleRows).ToList();
        }

        internal void UpdateTimelineRange(
            IList<DateTimeOffset> days, int viewportDays)
        {
            TimelineTotalDays = days == null ? 1 : Math.Max(1, days.Count - 1);
            TimelineMaximumStartDay = Math.Max(0,
                TimelineTotalDays - Math.Max(1, viewportDays));
            TimelineStartDay = Math.Max(0,
                Math.Min(TimelineMaximumStartDay, TimelineStartDay));
        }

        internal void MoveTimelineToLatest(
            IList<DateTimeOffset> days, int viewportDays)
        {
            UpdateTimelineRange(days, viewportDays);
            TimelineStartDay = TimelineMaximumStartDay;
        }

        internal bool BeginTimelineDrag(
            PointF point, RectangleF sliderBounds, RectangleF thumbBounds)
        {
            if (!ShowAll || TimelineMaximumStartDay <= 0 ||
                !sliderBounds.Contains(point))
            {
                return false;
            }

            DraggingTimelineSlider = true;
            TimelineSliderDragOffset = thumbBounds.Contains(point)
                ? point.X - thumbBounds.Left
                : thumbBounds.Width / 2f;
            return true;
        }

        internal void EndTimelineDrag()
        {
            DraggingTimelineSlider = false;
            TimelineSliderDragOffset = 0f;
        }

        internal bool UpdateTimelineFromSlider(float pointerX, float trackLeft,
            float trackWidth, float thumbWidth)
        {
            if (TimelineMaximumStartDay <= 0 || thumbWidth <= 0)
                return false;
            float available = Math.Max(1f, trackWidth - thumbWidth);
            float thumbLeft = Math.Max(trackLeft, Math.Min(trackLeft + available,
                pointerX - TimelineSliderDragOffset));
            int next = Convert.ToInt32(Math.Round(
                (thumbLeft - trackLeft) / available * TimelineMaximumStartDay));
            next = Math.Max(0, Math.Min(TimelineMaximumStartDay, next));
            if (next == TimelineStartDay)
                return false;
            TimelineStartDay = next;
            return true;
        }
    }
}
