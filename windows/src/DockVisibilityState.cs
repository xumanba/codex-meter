using System;

namespace CodexMeter
{
    internal sealed class DockVisibilityState
    {
        private DateTime? pointerLeftAt;
        private DateTime suppressRevealUntil;
        private DateTime suppressHideUntil;

        internal bool Revealed { get; private set; }
        internal bool IsAnimating { get; private set; }

        internal void Reveal(DateTime now, TimeSpan minimumVisibleDuration)
        {
            Revealed = true;
            pointerLeftAt = null;
            suppressHideUntil = now.Add(minimumVisibleDuration);
        }

        internal void Hide(DateTime now, TimeSpan revealDelay)
        {
            Revealed = false;
            pointerLeftAt = null;
            suppressRevealUntil = now.Add(revealDelay);
        }

        internal void SuppressHide(DateTime now, TimeSpan duration)
        {
            suppressHideUntil = now.Add(duration);
            pointerLeftAt = null;
        }

        internal void Clear()
        {
            Revealed = false;
            IsAnimating = false;
            pointerLeftAt = null;
            suppressRevealUntil = DateTime.MinValue;
            suppressHideUntil = DateTime.MinValue;
        }

        internal void SetAnimating(bool animating)
        {
            IsAnimating = animating;
        }

        internal bool Evaluate(DateTime now, bool atStrip,
            bool overRevealedCard, bool menuVisible)
        {
            bool before = Revealed;
            if (!Revealed && now >= suppressRevealUntil && atStrip)
            {
                Revealed = true;
                pointerLeftAt = null;
            }
            else if (Revealed && now < suppressHideUntil)
            {
                pointerLeftAt = null;
            }
            else if (Revealed && (overRevealedCard || atStrip || menuVisible))
            {
                pointerLeftAt = null;
            }
            else if (Revealed)
            {
                if (!pointerLeftAt.HasValue)
                    pointerLeftAt = now;
                else if ((now - pointerLeftAt.Value).TotalMilliseconds >= 220)
                {
                    Revealed = false;
                    pointerLeftAt = null;
                }
            }
            return before != Revealed;
        }
    }
}
