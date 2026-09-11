using HMUI;
using UnityEngine;
using UnityEngine.UI;
using VainSabers.Helpers;

namespace VainSabers.UI;

public class RawImageComponent : UIComponent
{
    private ImageView m_imageView = null!;
    private Sprite? m_cachedSprite;
    private Texture? m_cachedTexture;

    public Texture? Texture
    {
        get => m_cachedTexture;
        set
        {
            if (m_cachedTexture == value) return;
            m_cachedTexture = value;
            if (value is Texture2D tex2D)
            {
                // Reuse sprite if same texture, otherwise create new
                if (m_cachedSprite == null || m_cachedSprite.texture != tex2D)
                {
                    m_cachedSprite = Sprite.Create(tex2D, new Rect(0, 0, tex2D.width, tex2D.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Vector4.zero, false);
                    m_cachedSprite.texture.wrapMode = TextureWrapMode.Clamp;
                }
                m_imageView.sprite = m_cachedSprite;
            }
            else
            {
                m_imageView.sprite = null;
            }
        }
    }

    public Color Color
    {
        get => m_imageView.color;
        set => m_imageView.color = value;
    }

    public bool RaycastTarget
    {
        get => m_imageView.raycastTarget;
        set => m_imageView.raycastTarget = value;
    }

    protected override void Init()
    {
        base.Init();
        m_imageView = gameObject.RequireComponent<ImageView>();
        m_imageView.raycastTarget = false;
        m_imageView.color = Color.white;
        m_imageView.material = UIResources.NoGlowMat;
        m_imageView.type = Image.Type.Simple;
        m_imageView.preserveAspect = false;
    }
}
