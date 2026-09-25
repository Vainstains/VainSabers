using System;
using System.Collections.Generic;
using UnityEngine;

namespace VainSabers.UI;

/// <summary>
/// Combined element containing a dropdown and an edit button side-by-side.
/// Used to disambiguate which preset the edit button acts on when multiple
/// preset dropdowns are present (e.g. gameplay saber + menu saber).
/// </summary>
public class DropdownWithEditComponent : UIComponent
{
    private DropdownComponent m_dropdown = null!;
    private TextButtonComponent m_button = null!;

    public DropdownComponent Dropdown => m_dropdown;
    public TextButtonComponent EditButton => m_button;

    public event Action<int>? OnSelectionChanged
    {
        add => m_dropdown.OnSelectionChanged += value;
        remove => m_dropdown.OnSelectionChanged -= value;
    }

    public event Action? OnEditClicked
    {
        add => m_button.OnClick += value;
        remove => m_button.OnClick -= value;
    }

    protected override void Init()
    {
        base.Init();
        var row = AddChild<HorizontalLayoutGroupComponent>().ToFill();
        row.WithSpacing(1);
        row.ChildControlWidth = true;
        row.ChildControlHeight = true;
        row.ChildForceExpandWidth = false;
        row.ChildForceExpandHeight = true;

        m_dropdown = row.AddChild<DropdownComponent>();
        m_dropdown.LayoutElement.flexibleWidth = 1;
        m_dropdown.LayoutElement.preferredWidth = 0;

        m_button = row.AddChild<TextButtonComponent>().WithText("Edit");
        m_button.LayoutElement.preferredWidth = 15;
        m_button.LayoutElement.flexibleWidth = 0;
        m_button.Color = new Color(0.3f, 0.45f, 0.7f, 1f);
    }

    public void SetOptions(IEnumerable<string> options, int selectedIndex = 0)
    {
        m_dropdown.SetOptions(options, selectedIndex);
    }

    public void SetEnumOptions<T>(T selected) where T : struct, Enum
    {
        m_dropdown.SetEnumOptions(selected);
    }

    public int SelectedIndex
    {
        get => m_dropdown.SelectedIndex;
        set => m_dropdown.SelectedIndex = value;
    }

    public string? SelectedValue => m_dropdown.SelectedValue;

    public T SelectedEnumValue<T>() where T : struct, Enum => m_dropdown.SelectedEnumValue<T>();
}
