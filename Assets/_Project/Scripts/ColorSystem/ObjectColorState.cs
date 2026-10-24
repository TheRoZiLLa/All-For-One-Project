using System;

namespace Recorded.ColorSystem
{
    /// <summary>
    /// Flags-based primary color state for objects in RECORDED.
    /// Supports additive RGB color mixing:
    /// None (Neutral Grey), Red, Green, Blue,
    /// Red + Green (Yellow), Red + Blue (Magenta), Green + Blue (Cyan), Red + Green + Blue (White).
    /// </summary>
    [Flags]
    public enum ObjectColorState
    {
        None  = 0,
        Red   = 1 << 0, // 1
        Green = 1 << 1, // 2
        Blue  = 1 << 2  // 4
    }
}
