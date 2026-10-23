using System;

namespace Recorded.ColorSystem
{
    /// <summary>
    /// Interface for objects that can receive and manage RGB primary color states.
    /// Allows weapons and interactions to apply color without knowing specific object implementations.
    /// </summary>
    public interface IColorReceivable
    {
        ObjectColorState CurrentColorState { get; }
        event Action<ObjectColorState> OnColorStateChanged;
        void ApplyColor(ObjectColorState color);
        void ResetColors();
    }
}
