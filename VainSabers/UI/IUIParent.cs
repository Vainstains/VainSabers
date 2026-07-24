namespace VainSabers.UI;

public interface IUIParent
{
    public T AddChild<T>() where T : UIComponent;
}
