namespace VainSabers.Data;

public enum Easing
{
    Linear,
    EaseInQuad,
    EaseOutQuad,
    EaseInOutQuad
}

public static class EasingExtensions
{
    public static float Evaluate(this Easing easing, float t)
    {
        switch (easing)
        {
            case Easing.Linear:
                return t;
            case Easing.EaseInQuad:
                return t * t;
            case Easing.EaseOutQuad:
                return t * (2f - t);
            case Easing.EaseInOutQuad:
                return t < 0.5f
                    ? 2f * t * t
                    : -1f + (4f - 2f * t) * t;
            default:
                return t;
        }
    }
}