// Reference-only stubs for the sliver of UnityEditor that RUNTIME assemblies touch
// from inside #if UNITY_EDITOR guards.
//
// Unity auto-references UnityEditor when it compiles a runtime assembly for the
// editor, so this code is legal in the real project - it just has nowhere to resolve
// from in an offline Roslyn compile.
//
// This is deliberately tiny. It exists so the editor-guarded branches of runtime code
// are type-checked; it is NOT an attempt to model the editor API, and the project's
// own Editor assembly (BuildScript, TestTrackGenerator) is not compiled by the
// harness at all. See README.md.

namespace UnityEditor
{
    public static class EditorApplication
    {
        public static bool isPlaying { get; set; }
        public static bool isPaused { get; set; }
        public static bool isCompiling { get; }

        public static void Exit(int returnValue) { }
    }
}
