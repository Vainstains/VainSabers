using HMUI;
using UnityEngine;
using UnityEngine.UI;
using VainSabers.Helpers;

namespace VainSabers.UI;

public class RoundRectComponent : UIComponent
{
    private ImageView m_imageView = null!;

    public bool IsRaycastTarget
    {
        get => m_imageView.raycastTarget;
        set => m_imageView.raycastTarget = value;
    }

    public Color Color
    {
        get => m_imageView.color;
        set => m_imageView.color = value;
    }
    
    protected override void Init()
    {
        base.Init();
        m_imageView = gameObject.RequireComponent<ImageView>();
        m_imageView.raycastTarget = false;
        m_imageView.color = Color.white;
        m_imageView.sprite = UIResources.RoundSprite;
        m_imageView.material = UIResources.NoGlowMat;
        m_imageView.type = Image.Type.Sliced;
    }
}
