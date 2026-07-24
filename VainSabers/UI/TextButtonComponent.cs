using System;
using TMPro;
using UnityEngine;

namespace VainSabers.UI;

public class TextButtonComponent : ButtonComponent
{
    private TextComponent? m_textComponent;

    public TextComponent Text => m_textComponent ?? null!;

    protected override void UpdateState()
    {
        base.UpdateState();
        if (m_textComponent == null)
            return;
        m_textComponent.Color = IsInteractable ? new Color(0.9f, 0.9f, 0.9f, 1.0f) : new Color(0.7f, 0.7f, 0.7f, 0.8f);
    }

    protected override void Init()
    {
        base.Init();
        m_textComponent = AddChild<TextComponent>().ToFill();
        m_textComponent.Text = "Button";
        m_textComponent.Alignment = TextAlignmentOptions.Center;
        m_textComponent.OverflowMode = TextOverflowModes.Overflow;
        m_textComponent.EnableWordWrapping = false;

        UpdateState();
    }

    public TextButtonComponent WithText(string text)
    {
        if (m_textComponent == null)
            throw new Exception("shouldnt be possible (text component not initialized)");
        m_textComponent.Text = text;
        return this;
    }
}