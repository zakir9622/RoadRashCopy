using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HighwayRenegade.Editor
{
    /// <summary>
    /// Assigns private serialized fields on components the scene generators build.
    ///
    /// Every generated scene is wired through SerializedObject.FindProperty, because the
    /// fields being set are private and the generator is not allowed to widen a component's
    /// API just to construct it. FindProperty has one sharp edge: it returns <c>null</c> for
    /// a field Unity does not serialize, and the caller then dereferences that null.
    ///
    /// That is not hypothetical. PoliceAI._target was a plain private field, so
    /// TestTrackGenerator.BuildPolice threw a NullReferenceException six minutes into every
    /// Android build, and the message named a line number in the generator rather than the
    /// component or the field. Twenty-five minutes of CI to learn nothing.
    ///
    /// These helpers turn that into one sentence naming the component type and the field.
    /// Use them for every generator assignment; never call FindProperty directly.
    /// </summary>
    public static class SerializedWiring
    {
        /// <summary>Assigns an object reference, e.g. a Transform or another component.</summary>
        public static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            Find(so, target, field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            Find(so, target, field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            Find(so, target, field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetInt(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            Find(so, target, field).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Assigns an enum by its underlying value.</summary>
        public static void SetEnum(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            Find(so, target, field).enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Resolves a field, or throws saying which component and which field.
        ///
        /// Throwing rather than logging is deliberate. A generator that carries on after
        /// failing to wire a reference writes a scene that looks fine and is missing a
        /// connection nobody will notice until the feature silently does nothing in a
        /// shipped build - which is the exact failure this whole file exists to end.
        /// </summary>
        private static SerializedProperty Find(SerializedObject so, Object target, string field)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property != null) return property;

            throw new InvalidOperationException(
                $"[SerializedWiring] {target.GetType().Name} has no serialized field '{field}'. " +
                $"Add [SerializeField] to it, or correct the name in the generator. " +
                $"A private field without [SerializeField] is not serialized, so it cannot be " +
                $"wired here and would be discarded when the scene is saved.");
        }
    }
}
