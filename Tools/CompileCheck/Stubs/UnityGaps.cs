// Types that exist in the Unity editor/player API but are absent from the
// UnityEngine.Modules NuGet reference set the harness compiles against.
//
// Unlike the other stub files, these are not third-party packages - they are core
// Unity API that the published reference assemblies simply do not carry. Each one
// is transcribed from Unity's own source so the harness checks the real signature.

namespace UnityEngine
{
    /// <summary>
    /// Verified against Unity's published source:
    /// Runtime/Export/Handheld/Handheld.bindings.cs - "public class Handheld" with
    /// "public static extern void Vibrate()".
    /// </summary>
    public class Handheld
    {
        public static void Vibrate() { }
    }
}
