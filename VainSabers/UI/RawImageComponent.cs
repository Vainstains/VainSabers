using UnityEngine;
using UnityEngine.UI;
using VainSabers.Helpers;

namespace VainSabers.UI;

public class RawImageComponent : UIComponent
{
    private RawImage m_rawImage = null!;

    public Texture? Texture
    {
        get => m_rawImage.texture;
        set => m_rawImage.texture = value;
    }

    public Color Color
    {
        get => m_rawImage.color;
        set => m_rawImage.color = value;
    }

    public bool RaycastTarget
    {
        get => m_rawImage.raycastTarget;
        set => m_rawImage.raycastTarget = value;
    }

    protected override void Init()
    {
        base.Init();
        m_rawImage = gameObject.RequireComponent<RawImage>();
        m_rawImage.raycastTarget = false;
        m_rawImage.color = Color.white;
        m_rawImage.material = UIResources.NoGlowMat;
        m_rawImage.uvRect = new Rect(0, 0, 1, 1);
    }
}
