using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace VainSabers.Data;


[Serializable]
public class ColorGradientKey : IComparable<ColorGradientKey>
{
    public float Time;
    public Color Color;
    public Easing Easing;

    public ColorGradientKey(float time, Color color)
    {
        Time = time;
        Color = color;
    }

    public int CompareTo(ColorGradientKey other)
    {
        return Time.CompareTo(other.Time);
    }
}

[Serializable]
public class FloatGradientKey
{
    public float Time;
    public float Value;
    public Easing Easing;

    public FloatGradientKey(float time, float value)
    {
        Time = time;
        Value = value;
    }
}

public class ColorGradient
{
    // Nx1 texture
    private const int TextureSize = 128;
    public readonly List<ColorGradientKey> Keys = new();
    private Texture2D? m_gradientTexture;
    private bool m_gradientTextureDirty = true;

    public void SetColorKeys(List<ColorGradientKey> keys)
    {
        Keys.Clear();
        Keys.AddRange(keys);
        m_gradientTextureDirty = true;
    }

    public void SetFloatKeys(List<FloatGradientKey> keys)
    {
        Keys.Clear();
        foreach (var key in keys)
        {
            Keys.Add(new ColorGradientKey(key.Time, new Color(key.Value, key.Value, key.Value, 1f)) { Easing = key.Easing });
        }
        m_gradientTextureDirty = true;
    }

    // for serialization only (alloc)
    public List<FloatGradientKey> GetFloatKeys()
    {
        var floatKeys = new List<FloatGradientKey>();
        foreach (var key in Keys)
        {
            floatKeys.Add(new FloatGradientKey(key.Time, key.Color.r) { Easing = key.Easing });
        }
        return floatKeys;
    }

    public void SetDirty()
    {
        m_gradientTextureDirty = true;
    }

    private Color Evaluate(float t)
    {
        if (Keys.Count == 0)
            return Color.white;
        
        if (Keys.Count == 1)
            return Keys[0].Color;

        // assume Keys are sorted by time
        if (t <= Keys[0].Time)
            return Keys[0].Color;
        if (t >= Keys[Keys.Count - 1].Time)
            return Keys[Keys.Count - 1].Color;
        
        for (int i = 1; i < Keys.Count; i++)
        {
            var prev = Keys[i - 1];
            var next = Keys[i];
            if (t < next.Time)
            {
                var t0 = t - prev.Time;
                var interval = next.Time - prev.Time;
                var t1 = t0 / interval;
                return Color.Lerp(prev.Color, next.Color, next.Easing.Evaluate(t1));
            }
        }
        return Keys[Keys.Count - 1].Color;
    }

    private void UpdateGradientTexture()
    {
        if (!m_gradientTextureDirty)
            return;
        m_gradientTextureDirty = false;

        Keys.Sort((a, b) => a.Time.CompareTo(b.Time));

        if (m_gradientTexture == null)
        {
            m_gradientTexture = new Texture2D(TextureSize, 1, TextureFormat.RGBAFloat, false);
            m_gradientTexture.filterMode = FilterMode.Bilinear;
            m_gradientTexture.wrapMode = TextureWrapMode.Clamp;
        }

        var pixels = m_gradientTexture.GetRawTextureData<Color>();
        for (int i = 0; i < TextureSize; i++)
        {
            var t = i / (float)(TextureSize - 1);
            pixels[i] = Evaluate(t);
        }

        m_gradientTexture.Apply(false, false);
    }

    public Texture2D GetGradientTexture()
    {
        UpdateGradientTexture();
        return m_gradientTexture!;
    }
}