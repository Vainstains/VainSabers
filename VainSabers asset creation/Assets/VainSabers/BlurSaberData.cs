using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using VainSabers.Sabers;

namespace VainSabers
{
    public class BlurSaberData : MonoBehaviour
    {
        public Color CustomColor;
        public Material Normal, Inverted;
        public float BlurTime = 0.1f;
         private readonly List<BlurSaberPart> m_components = new List<BlurSaberPart>();
    public IReadOnlyList<BlurSaberPart> Components => m_components.AsReadOnly();
    public int ComponentCount => m_components.Count;
    

    public void RefreshComponentList()
    {
        m_components.Clear();
        foreach (Transform child in transform)
        {
            var part = child.GetComponent<BlurSaberPart>();
            if (part != null)
                m_components.Add(part);
        }
    }

    public BlurSaberPart AddComponent(string partName = "New Part")
    {
        var go = new GameObject(partName);
        go.transform.SetParent(transform, false);
        
        var newPart = go.AddComponent<BlurSaberPart>();
        newPart.Material = Normal;
        newPart.InvertedMaterial = Inverted;
        
        newPart.Length = 0.1f;
        newPart.StartRadius = 0.03f;
        newPart.EndRadius = 0.03f;
        newPart.StartColor = Color.white;
        newPart.EndColor = Color.white;
        newPart.StartCustomColorWeight = 1f;
        newPart.EndCustomColorWeight = 1f;
        newPart.StartGlow = 1f;
        newPart.EndGlow = 1f;
        newPart.Inverted = false;
        newPart.BlurFactor = 1f;
        newPart.BlurFadeFactor = 1f;
        newPart.EnableEndCaps = true;
        newPart.EndCapExtension = 0.25f;
        newPart.UseLookDir = false;
        newPart.LookDir = Vector3.zero;

        m_components.Add(newPart);
        return newPart;
    }

    public bool RemoveComponent(BlurSaberPart part, bool destroyGameObject = true)
    {
        if (part == null || !m_components.Contains(part))
            return false;

        m_components.Remove(part);

        if (destroyGameObject)
        {
#if UNITY_EDITOR
            if (Application.isEditor)
                DestroyImmediate(part.gameObject);
            else
                Destroy(part.gameObject);
#else
            Destroy(part.gameObject);
#endif
        }

        return true;
    }

    public bool RemoveComponentAt(int index, bool destroyGameObject = true)
    {
        if (index < 0 || index >= m_components.Count)
            return false;

        var part = m_components[index];
        return RemoveComponent(part, destroyGameObject);
    }

    public void RemoveAllComponents(bool destroyGameObjects = true)
    {
        var componentsToRemove = new List<BlurSaberPart>(m_components);
        foreach (var part in componentsToRemove)
            RemoveComponent(part, destroyGameObjects);
    }

    public BlurSaberPart FindComponent(string name)
    {
        return m_components.Find(part => part.gameObject.name == name);
    }

    public bool HasComponent(BlurSaberPart part)
    {
        return m_components.Contains(part);
    }

    public void ImportFromFile(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Debug.LogError($"File not found: {path}");
            return;
        }

        string[] lines = File.ReadAllLines(path);
        RemoveAllComponents();

        BlurSaberPart currentPart = null!;

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            if (line.StartsWith(".part"))
            {
                var partName = line.Length <= 6 ? "Part" : line.Substring(6).Trim();
                currentPart = AddComponent(partName);
                continue;
            }

            if (currentPart == null)
                continue;

            string[] tokens = line.Split(' ');
            string key = tokens[0];
            float[] vals = ParseFloats(tokens);

            switch (key)
            {
                case "pos":
                    currentPart.transform.localPosition = new Vector3(vals[0], vals[1], vals[2]);
                    break;
                case "rot":
                    currentPart.transform.localRotation = Quaternion.Euler(vals[0], vals[1], vals[2]);
                    break;
                case "length":
                    currentPart.Length = vals[0];
                    break;
                case "startRad":
                    currentPart.StartRadius = vals[0];
                    break;
                case "startColor":
                    currentPart.StartColor = new Color(vals[0], vals[1], vals[2], 1f);
                    break;
                case "startCustomWeight":
                    currentPart.StartCustomColorWeight = vals[0];
                    break;
                case "startGlow":
                    currentPart.StartGlow = vals[0];
                    break;
                case "endRad":
                    currentPart.EndRadius = vals[0];
                    break;
                case "endColor":
                    currentPart.EndColor = new Color(vals[0], vals[1], vals[2], 1f);
                    break;
                case "endCustomWeight":
                    currentPart.EndCustomColorWeight = vals[0];
                    break;
                case "endGlow":
                    currentPart.EndGlow = vals[0];
                    break;
                case "inverted":
                    currentPart.Inverted = Mathf.Approximately(vals[0], 1f);
                    break;
                case "blur":
                    currentPart.BlurFactor = Mathf.Clamp01(vals[0]);
                    break;
                case "blurFade":
                    currentPart.BlurFadeFactor = Mathf.Clamp(vals[0], 0f, 10f);
                    break;
                case "endCapExtension":
                    currentPart.EndCapExtension = Mathf.Clamp01(vals[0]);
                    break;
                case "enableEndCaps":
                    currentPart.EnableEndCaps = Mathf.Approximately(vals[0], 1f);
                    break;
                case "lookDir":
                    currentPart.LookDir = new Vector3(vals[0], vals[1], vals[2]);
                    break;
                case "useLookDir":
                    currentPart.UseLookDir = Mathf.Approximately(vals[0], 1f);
                    break;
                case "bulgeAmount":
                    currentPart.BulgeAmount = Mathf.Clamp(vals[0], -1f, 1f);
                    break;
                case "minimumRings":
                    currentPart.MinimumRings = Mathf.Clamp((int)vals[0], 2, 10);
                    break;
                case "renderQueueOffset":
                    currentPart.RenderQueueOffset = Mathf.RoundToInt(vals[0]);
                    break;
            }
        }

        Debug.Log($"Imported saber with {ComponentCount} parts from {path}");
    }

        private float[] ParseFloats(string[] tokens)
        {
            float[] vals = new float[tokens.Length - 1];
            for (int i = 1; i < tokens.Length; i++)
                vals[i - 1] = float.Parse(tokens[i], CultureInfo.InvariantCulture);
            return vals;
        }
    }
}

/*

.part
pos 1.0 2.0 3.0
rot 90.0 0.0 90.0
length 0.05
startRad 0.001
startColor 0.001 0.05 1.0
startCustomWeight 0.5
startGlow 0.9
endRad 0.001
endColor 0.001 0.05 1.0
endCustomWeight 0.5
endGlow 0.9
inverted 0

.part
...


*/