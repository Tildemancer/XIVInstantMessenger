// TildeTools: written for this fork, not part of upstream Messenger.

namespace Messenger.Configuration;

// The ranges the settings tabs offer, which a ctrl-clicked slider or a hand-edited config can go past
// Applied when the config loads and after every settings draw
// Maximum lines keeps upstream's own 1 to 30, though its slider shows 1 to 10
internal static class ConfigRanges
{
    internal static void Clamp(Config c)
    {
        c.ContextMenuPriority.ValidateRange(-100, 100);
        c.DoubleEnterDelay.ValidateRange(200, 500);
        c.AutoSaveInterval.ValidateRange(10, 60);
        c.HistoryAmount.ValidateRange(0, 5000);
        c.DisplayedMessages.ValidateRange(0, 200);
        c.IntervalBetweenSends.ValidateRange(200, 2000);
        c.TransMin.ValidateRange(0.05f, 1f);
        c.TransMax.ValidateRange(c.TransMin, 1f);
        c.TransDelta.ValidateRange(0f, 1f);
        c.DefaultSize.X.ValidateRange(200f, 2000f);
        c.DefaultSize.Y.ValidateRange(200f, 2000f);
        c.WindowCascadingReset.ValidateRange(1, 1000);
        c.WindowCascadingMaxColumns.ValidateRange(1, 100);
        c.PMLMaxLines.ValidateRange(1, 30);
        c.MessageLineSpacing.ValidateRange(-5, 5);
        c.MessageSpacing.ValidateRange(0, 5);
    }
}
