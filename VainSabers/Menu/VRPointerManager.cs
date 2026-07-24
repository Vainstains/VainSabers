using System.Reflection;
using UnityEngine;
using VRUIControls;
using Zenject;

namespace VainSabers.Menu;

public class VRPointerManager : IInitializable
{
    private VRPointer? m_pointer;
    private VRController? m_leftController;
    private VRController? m_rightController;

    public static VRPointerManager? Instance { get; private set; }

    public Transform? LeftRayTransform => m_leftController?.transform;
    public Transform? RightRayTransform => m_rightController?.transform;

    public Transform? ActiveTransform
    {
        get
        {
            var controller = m_pointer?.lastSelectedVrController;
            return controller?.transform;
        }
    }

    public VRPointerManager()
    {
        Instance = this;
    }

    public void Initialize()
    {
        var pointers = Resources.FindObjectsOfTypeAll<VRPointer>();
        if (pointers.Length == 0) return;

        m_pointer = pointers[0];

        m_leftController = m_pointer._leftVRController;
        m_rightController = m_pointer._rightVRController;
    }
}
