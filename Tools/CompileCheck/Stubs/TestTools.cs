// Reference-only stubs for com.unity.test-framework (1.4.5) UnityEngine.TestTools.
//
// nunit.framework itself comes from NuGet (the real assembly), so assertions are
// checked for real. Only the Unity-specific attributes and helpers live here.
//
// These types are NEVER compiled into a Unity build; Unity resolves the real package.
using System;
using System.Collections;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace UnityEngine.TestTools
{
    /// <summary>Marks a coroutine test that is driven a frame at a time by the player loop.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class UnityTestAttribute : NUnitAttribute, ISimpleTestBuilder, IImplyFixture
    {
        public TestMethod BuildFrom(IMethodInfo method, Test suite) => null;
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class UnitySetUpAttribute : NUnitAttribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class UnityTearDownAttribute : NUnitAttribute
    {
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class UnityPlatformAttribute : NUnitAttribute
    {
        public RuntimePlatform[] include { get; set; }
        public RuntimePlatform[] exclude { get; set; }
    }

    /// <summary>Fails a test if the expected console output does not appear.</summary>
    public static class LogAssert
    {
        public static bool ignoreFailingMessages { get; set; }

        public static void Expect(LogType type, string message) { }
        public static void Expect(LogType type, System.Text.RegularExpressions.Regex message) { }
        public static void NoUnexpectedReceived() { }
    }

    public interface IPrebuildSetup
    {
        void Setup();
    }

    public interface IPostBuildCleanup
    {
        void Cleanup();
    }

    public static class TestTools
    {
    }
}

namespace UnityEngine.TestTools.Utils
{
    public class FloatEqualityComparer : System.Collections.Generic.IEqualityComparer<float>
    {
        public static FloatEqualityComparer Instance { get; }
        public FloatEqualityComparer() { }
        public FloatEqualityComparer(float allowedError) { }
        public bool Equals(float expected, float actual) => false;
        public int GetHashCode(float value) => 0;
    }

    public class Vector3EqualityComparer : System.Collections.Generic.IEqualityComparer<Vector3>
    {
        public static Vector3EqualityComparer Instance { get; }
        public Vector3EqualityComparer() { }
        public Vector3EqualityComparer(float allowedError) { }
        public bool Equals(Vector3 expected, Vector3 actual) => false;
        public int GetHashCode(Vector3 value) => 0;
    }
}
