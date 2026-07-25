using HMUI;
using UnityEngine;
using UnityEngine.UI;
using VainSabers.Helpers;

namespace VainSabers.UI;

public class ImageComponent : UIComponent
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

    public Sprite? Sprite
    {
        get => m_imageView.sprite;
        set => m_imageView.sprite = value;
    }

    public Image.Type Type
    {
        get => m_imageView.type;
        set => m_imageView.type = value;
    }

    public bool PreserveAspect
    {
        get => m_imageView.preserveAspect;
        set => m_imageView.preserveAspect = value;
    }

    public ImageComponent AsSliced()
    {
        m_imageView.type = Image.Type.Sliced;
        return this;
    }

    public ImageComponent AsTiled()
    {
        m_imageView.type = Image.Type.Tiled;
        return this;
    }

    public ImageComponent AsFilled()
    {
        m_imageView.type = Image.Type.Filled;
        return this;
    }

    protected override void Init()
    {
        base.Init();
        m_imageView = gameObject.RequireComponent<ImageView>();
        m_imageView.raycastTarget = false;
        m_imageView.color = Color.white;
        m_imageView.sprite = UIResources.LoadSingleColorSprite(Color.white);
        m_imageView.material = UIResources.NoGlowMat;
    }
}
