using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace VainSabers.Helpers;

public static class Helpers
{
    public static TComponent AddInitComponent<TComponent>(
        this GameObject self, 
        params object[] args
    ) where TComponent : Component
    {
        var comp = self.AddComponent<TComponent>();
        
        var method = typeof(TComponent).GetMethod(
            "Init",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (method != null)
        {
            try
            {
                method.Invoke(comp, args);
            }
            catch (TargetParameterCountException)
            {
                Plugin.Log.Error(
                    $"Init(...) on {typeof(TComponent).Name} expects {method?.GetParameters().Length} parameters, " +
                    $"but {args.Length} were provided."
                );
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Init(...) invocation on {typeof(TComponent).Name} failed: {ex}");
            }
        }

        return comp;
    }
    
    public static TComponent AddInitChild<TComponent>(
        this GameObject self, 
        params object[] args
    ) where TComponent : Component
    {
        var childGo = new GameObject(typeof(TComponent).Name);
        childGo.transform.SetParent(self.transform, false);

        return childGo.AddInitComponent<TComponent>(args);
    }
}

public class UnityConstructorAttribute : Attribute;

// not needed at all but I really wanna be lazy and have implicit conversions
// with tuples and strings :p
public record struct VainColor(float r, float g, float b, float a = 1f)
{
    private static VainColor MakeColor((float r, float g, float b, float a) tuple) => new(tuple.r, tuple.g, tuple.b, tuple.a);
    private static VainColor MakeColor((float r, float g, float b) tuple) => new(tuple.r, tuple.g, tuple.b, 1f);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ParseHexChar(char c)
    {
        if (c >= '0' && c <= '9')
            return (byte)(c - '0');
        if (c >= 'a' && c <= 'f')
            return (byte)(c - 'a' + 10);
        if (c >= 'A' && c <= 'F')
            return (byte)(c - 'A' + 10);
        throw new ArgumentException("Invalid hex char");
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ParseHexByte(char c1, char c2)
    {
        var b1 = ParseHexChar(c1);
        var b2 = ParseHexChar(c2);
        return (byte)((b1 << 4) | b2);
    }

    private static VainColor MakeColor(string hex)
    {
        hex = hex.Trim().ToLower();
        switch (hex)
        {
            case "red":
                return new(1f, 0f, 0f);
            case "yellow":
                return new(1f, 1f, 0f);
            case "green":
                return new(0f, 1f, 0f);
            case "cyan":
                return new(0f, 1f, 1f);
            case "blue":
                return new(0f, 0f, 1f);
            case "magenta":
                return new(1f, 0f, 1f);
            case "white":
                return new(1f, 1f, 1f);
            case "black":
                return new(0f, 0f, 0f);
            case "gray":
                return new(0.5f, 0.5f, 0.5f);
            case "clear":
                return new(0f, 0f, 0f, 0f);
            case "transparent":
                return new(0f, 0f, 0f, 0f);
        }

        if (!hex.StartsWith("#"))
            throw new ArgumentException("Invalid hex string");

        hex = hex.Substring(1);

        var r = (byte)0;
        var g = (byte)0;
        var b = (byte)0;
        var a = (byte)255;

        if (hex.Length == 3)
        {
            r = ParseHexByte(hex[0], hex[0]);
            g = ParseHexByte(hex[1], hex[1]);
            b = ParseHexByte(hex[2], hex[2]);
        }
        else if (hex.Length == 4)
        {
            r = ParseHexByte(hex[0], hex[0]);
            g = ParseHexByte(hex[1], hex[1]);
            b = ParseHexByte(hex[2], hex[2]);
            a = ParseHexByte(hex[3], hex[3]);
        }
        else if (hex.Length == 6)
        {
            r = ParseHexByte(hex[0], hex[1]);
            g = ParseHexByte(hex[2], hex[3]);
            b = ParseHexByte(hex[4], hex[5]);
        }
        else if (hex.Length == 8)
        {
            r = ParseHexByte(hex[0], hex[1]);
            g = ParseHexByte(hex[2], hex[3]);
            b = ParseHexByte(hex[4], hex[5]);
            a = ParseHexByte(hex[6], hex[7]);
        }

        return new(r / 255f, g / 255f, b / 255f, a / 255f);
    }

    public static implicit operator Color(VainColor color) => new(color.r, color.g, color.b, color.a);
    public static implicit operator VainColor(Color color) => new(color.r, color.g, color.b, color.a);

    public static implicit operator VainColor((float r, float g, float b, float a) tuple) => MakeColor(tuple);
    public static implicit operator VainColor((float r, float g, float b) tuple) => MakeColor(tuple);
    public static implicit operator VainColor(string hex) => MakeColor(hex);

    #region Operator stuff
    public static VainColor operator +(VainColor a, VainColor b) => new(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
    public static VainColor operator -(VainColor a, VainColor b) => new(a.r - b.r, a.g - b.g, a.b - b.b, a.a - b.a);
    public static VainColor operator *(VainColor a, VainColor b) => new(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);

    public static VainColor operator +(VainColor a, float b) => new(a.r + b, a.g + b, a.b + b, a.a + b);
    public static VainColor operator -(VainColor a, float b) => new(a.r - b, a.g - b, a.b - b, a.a - b);
    public static VainColor operator *(VainColor a, float b) => new(a.r * b, a.g * b, a.b * b, a.a * b);
    public static VainColor operator /(VainColor a, float b) => new(a.r / b, a.g / b, a.b / b, a.a / b);

    public static VainColor operator +(float a, VainColor b) => new(a + b.r, a + b.g, a + b.b, a + b.a);
    public static VainColor operator -(float a, VainColor b) => new(a - b.r, a - b.g, a - b.b, a - b.a);
    public static VainColor operator *(float a, VainColor b) => new(a * b.r, a * b.g, a * b.b, a * b.a);
    public static VainColor operator /(float a, VainColor b) => new(a / b.r, a / b.g, a / b.b, a / b.a);

    public static VainColor operator +(VainColor a, Color b) => new(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
    public static VainColor operator +(Color a, VainColor b) => new(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
    public static VainColor operator +(VainColor a, string b) => a + MakeColor(b);
    public static VainColor operator +(string a, VainColor b) => MakeColor(a) + b;
    public static VainColor operator +(VainColor a, (float r, float g, float b, float a) b) => a + MakeColor(b);
    public static VainColor operator +((float r, float g, float b, float a) a, VainColor b) => MakeColor(a) + b;
    public static VainColor operator +(VainColor a, (float r, float g, float b) b) => a + MakeColor(b);
    public static VainColor operator +((float r, float g, float b) a, VainColor b) => MakeColor(a) + b;

    public static VainColor operator -(VainColor a, Color b) => new(a.r - b.r, a.g - b.g, a.b - b.b, a.a - b.a);
    public static VainColor operator -(Color a, VainColor b) => new(a.r - b.r, a.g - b.g, a.b - b.b, a.a - b.a);
    public static VainColor operator -(VainColor a, string b) => a - MakeColor(b);
    public static VainColor operator -(string a, VainColor b) => MakeColor(a) - b;
    public static VainColor operator -(VainColor a, (float r, float g, float b, float a) b) => a - MakeColor(b);
    public static VainColor operator -((float r, float g, float b, float a) a, VainColor b) => MakeColor(a) - b;
    public static VainColor operator -(VainColor a, (float r, float g, float b) b) => a - MakeColor(b);
    public static VainColor operator -((float r, float g, float b) a, VainColor b) => MakeColor(a) - b;

    public static VainColor operator *(VainColor a, Color b) => new(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
    public static VainColor operator *(Color a, VainColor b) => new(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
    public static VainColor operator *(VainColor a, string b) => a * MakeColor(b);
    public static VainColor operator *(string a, VainColor b) => MakeColor(a) * b;
    public static VainColor operator *(VainColor a, (float r, float g, float b, float a) b) => a * MakeColor(b);
    public static VainColor operator *((float r, float g, float b, float a) a, VainColor b) => MakeColor(a) * b;
    public static VainColor operator *(VainColor a, (float r, float g, float b) b) => a * MakeColor(b);
    public static VainColor operator *((float r, float g, float b) a, VainColor b) => MakeColor(a) * b;
    #endregion

    public override string ToString() => $"({r:0.00}, {g:0.00}, {b:0.00}, {a:0.00})";
}