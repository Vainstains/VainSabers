using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VainSabers.Menu;

public class MenuPointers
{
    private GameObject LeftPointer = null!;
    private GameObject RightPointer = null!;
    
    public (Transform leftParent, Transform rightParent) Parents
    {
        get
        {
            if (LeftPointer == null)
                LeftPointer = Resources.FindObjectsOfTypeAll<VRController>().First(c => c.transform.name == "ControllerLeft").transform.Find("MenuHandle").gameObject;
            if (RightPointer == null)
                RightPointer = Resources.FindObjectsOfTypeAll<VRController>().First(c => c.transform.name == "ControllerRight").transform.Find("MenuHandle").gameObject;
            
            return (LeftPointer.transform, RightPointer.transform);
        }
    }

    public void SetPointerVisibility(bool visible)
    {
        var controllers = Resources.FindObjectsOfTypeAll<VRController>();
        LeftPointer = controllers.First(c => c.transform.name == "ControllerLeft").transform.Find("MenuHandle").gameObject;
        RightPointer = controllers.First(c => c.transform.name == "ControllerRight").transform.Find("MenuHandle").gameObject;

        GetMenuHandleRenderers(LeftPointer).ForEach(r => { if (r != null) r.enabled = visible; });
        GetMenuHandleRenderers(RightPointer).ForEach(r => { if (r != null) r.enabled = visible; });
    }

    private static List<MeshRenderer> GetMenuHandleRenderers(GameObject menuHandle) => [
        menuHandle.transform.Find("Glowing").GetComponent<MeshRenderer>(),
        menuHandle.transform.Find("Normal").GetComponent<MeshRenderer>(),
        menuHandle.transform.Find("FakeGlow0").GetComponent<MeshRenderer>(),
        menuHandle.transform.Find("FakeGlow1").GetComponent<MeshRenderer>()
    ];
}