using System;

namespace VainSabers.Helpers;

[AttributeUsage(AttributeTargets.Field)]
public class LabelAttribute : Attribute
{
    public string Text { get; }

    public LabelAttribute(string text)
    {
        Text = text;
    }
}
