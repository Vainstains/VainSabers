using UnityEngine;

namespace VainSabers.UI;

public static class UIExtensions
{
    public static T Move<T>(this T component, Vector2 delta) where T : UIComponent
    {
        component.RectTransform.anchoredPosition += delta;
        return component;
    }

    public static T Move<T>(this T component, float deltaX, float deltaY) where T : UIComponent
    {
        component.RectTransform.anchoredPosition += new Vector2(deltaX, deltaY);
        return component;
    }

    public static T ExtendTop<T>(this T component, float delta) where T : UIComponent
    {
        component.RectTransform.offsetMax += new Vector2(0, delta);
        return component;
    }

    public static T ExtendBottom<T>(this T component, float delta) where T : UIComponent
    {
        component.RectTransform.offsetMin += new Vector2(0, -delta);
        return component;
    }

    public static T ExtendLeft<T>(this T component, float delta) where T : UIComponent
    {
        component.RectTransform.offsetMin += new Vector2(-delta, 0);
        return component;
    }

    public static T ExtendRight<T>(this T component, float delta) where T : UIComponent
    {
        component.RectTransform.offsetMax += new Vector2(delta, 0);
        return component;
    }

    public static T InsetTop<T>(this T component, float delta) where T : UIComponent
        => component.ExtendTop(-delta);
    public static T InsetBottom<T>(this T component, float delta) where T : UIComponent
        => component.ExtendBottom(-delta);
    public static T InsetLeft<T>(this T component, float delta) where T : UIComponent
        => component.ExtendLeft(-delta);
    public static T InsetRight<T>(this T component, float delta) where T : UIComponent
        => component.ExtendRight(-delta);

    public static T Extend<T>(this T component, float delta) where T : UIComponent
        => component.ExtendTop(delta).ExtendBottom(delta).ExtendLeft(delta).ExtendRight(delta);

    public static T Inset<T>(this T component, float delta) where T : UIComponent
        => component.InsetTop(delta).InsetBottom(delta).InsetLeft(delta).InsetRight(delta);
    
    public static T SetAnchors<T>(this T component, Vector2 min, Vector2 max) where T : UIComponent
    {
        component.RectTransform.anchorMin = min;
        component.RectTransform.anchorMax = max;
        return component;
    }

    public static T SetOffsets<T>(this T component, Vector2 min, Vector2 max) where T : UIComponent
    {
        component.RectTransform.offsetMin = min;
        component.RectTransform.offsetMax = max;
        return component;
    }

    public static T ClearOffsets<T>(this T component) where T : UIComponent
    {
        component.RectTransform.offsetMin = Vector2.zero;
        component.RectTransform.offsetMax = Vector2.zero;
        return component;
    }

    public static T ToTopCenter<T>(this T component) where T : UIComponent =>
        component.SetAnchors(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f)).ClearOffsets();
    public static T ToBottomCenter<T>(this T component) where T : UIComponent =>
        component.SetAnchors(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)).ClearOffsets();
    public static T ToLeftCenter<T>(this T component) where T : UIComponent =>
        component.SetAnchors(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f)).ClearOffsets();
    public static T ToRightCenter<T>(this T component) where T : UIComponent =>
        component.SetAnchors(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f)).ClearOffsets();
    public static T ToCenter<T>(this T component) where T : UIComponent =>
        component.SetAnchors(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).ClearOffsets();
    
    public static T ToTopLeft<T>(this T component) where T : UIComponent =>
        component.SetAnchors(new Vector2(0f, 1f), new Vector2(0f, 1f)).ClearOffsets();
    public static T ToTopRight<T>(this T component) where T : UIComponent =>
        component.SetAnchors(new Vector2(1f, 1f), new Vector2(1f, 1f)).ClearOffsets();
    public static T ToBottomLeft<T>(this T component) where T : UIComponent =>
        component.SetAnchors(new Vector2(0f, 0f), new Vector2(0f, 0f)).ClearOffsets();
    public static T ToBottomRight<T>(this T component) where T : UIComponent =>
        component.SetAnchors(new Vector2(1f, 0f), new Vector2(1f, 0f)).ClearOffsets();
    
    public static T ToTopEdge<T>(this T component) where T : UIComponent =>
        component.SetAnchors(new Vector2(0f, 1f), new Vector2(1f, 1f)).ClearOffsets();
    public static T ToBottomEdge<T>(this T component) where T : UIComponent =>
        component.SetAnchors(new Vector2(0f, 0f), new Vector2(1f, 0f)).ClearOffsets();
    public static T ToLeftEdge<T>(this T component) where T : UIComponent =>
        component.SetAnchors(new Vector2(0f, 0f), new Vector2(0f, 1f)).ClearOffsets();
    public static T ToRightEdge<T>(this T component) where T : UIComponent =>
        component.SetAnchors(new Vector2(1f, 0f), new Vector2(1f, 1f)).ClearOffsets();
    
    public static T ToFill<T>(this T component) where T : UIComponent =>
        component.SetAnchors(Vector2.zero, Vector2.one).ClearOffsets();
}
