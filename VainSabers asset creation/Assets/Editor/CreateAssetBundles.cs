using UnityEditor;

public class CreateAssetBundles
{
    [MenuItem ("Tools/Build AssetBundles")]
    static void BuildAllAssetBundles ()
    {
        BuildPipeline.BuildAssetBundles("C:\\Users\\dbasp\\RiderProjects\\VainSabers\\VainSabers", BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);
    }
}