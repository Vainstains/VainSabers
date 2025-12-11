using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using VainSabers.Sabers;

#if UNITY_EDITOR
namespace VainSabers
{
    [CustomEditor(typeof(BlurSaberData))]
    public class BlurSaberDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            BlurSaberData data = (BlurSaberData)target;

            GUILayout.Space(10);

            if (GUILayout.Button("Export Blur Saber Parts"))
            {
                ExportParts(data);
            }

            if (GUILayout.Button("Import Blur Saber Parts"))
            {
                string path = EditorUtility.OpenFilePanel("Import Blur Saber Parts", Application.dataPath, "txt");
                if (!string.IsNullOrEmpty(path))
                {
                    data.ImportFromFile(path);
                }
            }
        }

        private void ExportParts(BlurSaberData data)
        {
            var parts = data.GetComponentsInChildren<BlurSaberPart>();
            if (parts.Length == 0)
            {
                Debug.LogWarning("No BlurSaberPart components found under this BlurSaberData.");
                return;
            }

            StringBuilder sb = new StringBuilder();

            foreach (var part in parts)
            {
                // Local position/rotation relative to the data object
                Vector3 localPos = data.transform.InverseTransformPoint(part.transform.position);
                Quaternion localRot = Quaternion.Inverse(data.transform.rotation) * part.transform.rotation;
                Vector3 euler = localRot.eulerAngles;

                sb.AppendLine($".part {part.gameObject.name}");
                sb.AppendLine($"pos {localPos.x:F4} {localPos.y:F4} {localPos.z:F4}");
                sb.AppendLine($"rot {euler.x:F4} {euler.y:F4} {euler.z:F4}");
                sb.AppendLine($"length {FormatFloat(part.Length)}");

                sb.AppendLine($"startRad {FormatFloat(part.StartRadius)}");
                sb.AppendLine($"startColor {FormatFloat(part.StartColor.r)} {FormatFloat(part.StartColor.g)} {FormatFloat(part.StartColor.b)}");
                sb.AppendLine($"startCustomWeight {FormatFloat(part.StartCustomColorWeight)}");
                sb.AppendLine($"startGlow {FormatFloat(part.StartGlow)}");

                sb.AppendLine($"endRad {FormatFloat(part.EndRadius)}");
                sb.AppendLine($"endColor {FormatFloat(part.EndColor.r)} {FormatFloat(part.EndColor.g)} {FormatFloat(part.EndColor.b)}");
                sb.AppendLine($"endCustomWeight {FormatFloat(part.EndCustomColorWeight)}");
                sb.AppendLine($"endGlow {FormatFloat(part.EndGlow)}");

                sb.AppendLine($"inverted {(part.Inverted ? "1" : "0")}");
                sb.AppendLine($"blur {FormatFloat(part.BlurFactor)}");
                sb.AppendLine($"blurFade {FormatFloat(part.BlurFadeFactor)}");
                sb.AppendLine($"enableEndCaps {(part.EnableEndCaps ? "1" : "0")}");
                sb.AppendLine($"endCapExtension {FormatFloat(part.EndCapExtension)}");

                sb.AppendLine($"lookDir {FormatFloat(part.LookDir.x)} {FormatFloat(part.LookDir.y)} {FormatFloat(part.LookDir.z)}");
                sb.AppendLine($"useLookDir {(part.UseLookDir ? "1" : "0")}");
            
                sb.AppendLine($"bulgeAmount {FormatFloat(part.BulgeAmount)}");
                sb.AppendLine($"minimumRings {part.MinimumRings}");
                sb.AppendLine($"renderQueueOffset {part.RenderQueueOffset}");

                sb.AppendLine();
            }

            string path = EditorUtility.SaveFilePanel(
                "Save Blur Saber Parts",
                Application.dataPath,
                "BlurSaberParts.txt",
                "txt"
            );

            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllText(path, sb.ToString());
                Debug.Log($"Exported {parts.Length} parts to {path}");
            }
        }
        private string FormatFloat(float value)
        {
            return value.ToString("F6", CultureInfo.InvariantCulture);
        }
    }
}
#endif