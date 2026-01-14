using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using VainSabers.Config;

namespace VainSabers.Sabers;

public class BlurSaberData : MonoBehaviour
{
    private PluginConfig? m_config = null;
    public Color CustomColor;
    public float BlurTime => m_config != null ? m_config.BlurMS * 0.001f : 0.04f;
    
    private readonly List<BlurSaberPart> m_components = new List<BlurSaberPart>();
    public IReadOnlyList<BlurSaberPart> Components => m_components.AsReadOnly();
    public int ComponentCount => m_components.Count;

    public void Init(PluginConfig config)
    {
        m_config = config;
    }

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
        newPart.Material = VainSabersAssets.NormalSaberMaterial!;
        newPart.InvertedMaterial = VainSabersAssets.InvertedSaberMaterial!;
        newPart.LitMaterial = VainSabersAssets.NormalLitSaberMaterial!;
        newPart.LitInvertedMaterial = VainSabersAssets.InvertedLitSaberMaterial!;
        
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
        newPart.Lit = false;
        
        newPart.Config = m_config!;

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
                    currentPart.RotX = vals[0];
                    currentPart.RotY = vals[1];
                    currentPart.RotZ = vals[2];
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
                    currentPart.EndCapExtension = Mathf.Clamp(vals[0], 0.0f, 3.0f);
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
                case "lit":
                    currentPart.Lit = Mathf.Approximately(vals[0], 1f);
                    break;
                case "hueShift":
                    currentPart.HueShift = vals[0];
                    break;
            }
        }

        Debug.Log($"Imported saber with {ComponentCount} parts from {path}");
    }

    public void SaveToFile(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("Save path cannot be null or empty");
            return;
        }

        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < m_components.Count; i++)
        {
            var part = m_components[i];
            if (part == null) continue;

            sb.AppendLine($".part {part.gameObject.name}");

            Vector3 pos = part.transform.localPosition;
            sb.AppendLine($"pos {FormatFloat(pos.x)} {FormatFloat(pos.y)} {FormatFloat(pos.z)}");

            Vector3 rot = part.transform.localEulerAngles;
            sb.AppendLine($"rot {FormatFloat(rot.x)} {FormatFloat(rot.y)} {FormatFloat(rot.z)}");

            sb.AppendLine($"length {FormatFloat(part.Length)}");
            
            sb.AppendLine($"hueShift {FormatFloat(part.HueShift)}");

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
            
            sb.AppendLine($"lit {(part.Lit ? "1" : "0")}");

            sb.AppendLine();
        }

        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, sb.ToString());
            Debug.Log($"Saved saber with {ComponentCount} parts to {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save saber to {path}: {ex.Message}");
        }
    }

    private string FormatFloat(float value)
    {
        return value.ToString("F6", CultureInfo.InvariantCulture);
    }

    private float[] ParseFloats(string[] tokens)
    {
        float[] vals = new float[tokens.Length - 1];
        for (int i = 1; i < tokens.Length; i++)
            vals[i - 1] = float.Parse(tokens[i], CultureInfo.InvariantCulture);
        return vals;
    }
}