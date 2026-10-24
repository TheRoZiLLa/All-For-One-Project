using UnityEngine;

namespace Recorded.ColorSystem
{
    /// <summary>
    /// Any prop the Spectrum Gun may paint. The gun looks up this interface only.
    /// It does not call GetComponent for a concrete target type.
    /// </summary>
    public interface IColorTarget : IColorReceivable
    {
        Rigidbody PhysicsBody { get; }
        bool IsPuzzleCritical { get; }
    }
}
