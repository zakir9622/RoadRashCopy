namespace HighwayRenegade.Core.Combat
{
    /// <summary>
    /// Road Rash-style rider stamina: fighting and taking hits drain it; empty stamina
    /// increases crash severity and slows remount.
    /// </summary>
    public static class StaminaRules
    {
        public const float Max = 100f;
        public const float RecoverPerSec = 8f;
        public const float SwingCost = 6f;
        public const float HitTakenCost = 14f;
        public const float KickCost = 10f;

        public static float ApplySwing(float current) =>
            Clamp(current - SwingCost);

        public static float ApplyKick(float current) =>
            Clamp(current - KickCost);

        public static float ApplyHit(float current) =>
            Clamp(current - HitTakenCost);

        public static float Recover(float current, float deltaSeconds) =>
            Clamp(current + RecoverPerSec * deltaSeconds);

        public static bool IsExhausted(float current) => current <= 12f;

        private static float Clamp(float v)
        {
            if (v < 0f) return 0f;
            if (v > Max) return Max;
            return v;
        }
    }
}
