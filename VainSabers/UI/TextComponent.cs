using HMUI;
using TMPro;
using UnityEngine;
using VainSabers.Helpers;

namespace VainSabers.UI;

public class TextComponent : UIComponent
{
    private CurvedTextMeshPro m_textMeshPro = null!;

    public Color Color
    {
        get => m_textMeshPro.color;
        set => m_textMeshPro.color = value;
    }

    public string Text
    {
        get => m_textMeshPro.text;
        set
        {
            m_textMeshPro.text = value;
        }
    }

    public float FontSize
    {
        get => m_textMeshPro.fontSize;
        set => m_textMeshPro.fontSize = value;
    }

    public TextAlignmentOptions Alignment
    {
        get => m_textMeshPro.alignment;
        set => m_textMeshPro.alignment = value;
    }

    public TextOverflowModes OverflowMode
    {
        get => m_textMeshPro.overflowMode;
        set => m_textMeshPro.overflowMode = value;
    }

    public bool EnableWordWrapping
    {
        get => m_textMeshPro.enableWordWrapping;
        set => m_textMeshPro.enableWordWrapping = value;
    }

    protected override void Init()
    {
        base.Init();
        m_textMeshPro = gameObject.RequireComponent<CurvedTextMeshPro>();
        m_textMeshPro.font = UIResources.GameFont;
        m_textMeshPro.fontSharedMaterial = UIResources.GameFontMaterial;
        m_textMeshPro.color = Color.white;
        m_textMeshPro.fontSize = 3f;
        m_textMeshPro.alignment = TextAlignmentOptions.TopLeft;
        m_textMeshPro.overflowMode = TextOverflowModes.Overflow;
        m_textMeshPro.enableWordWrapping = true;
    }
}
