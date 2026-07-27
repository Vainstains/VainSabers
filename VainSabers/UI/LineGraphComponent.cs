using System;
using System.Collections.Generic;
using HMUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VainSabers.Helpers;
using VainSabers.Menu;

namespace VainSabers.UI;

public class LineGraphComponent : UIComponent
{
    private const float DefaultDragSensitivity = 0.5f;

    private MeshFilter? m_meshFilter;
    private MeshRenderer? m_meshRenderer;
    private Mesh? m_mesh;

    private readonly List<GraphPointComponent> m_pointChildren = new();
    private readonly List<Vector2> m_dataPoints = new();

    private Color m_lineColor = Color.white;
    private float m_lineWidth = 2f;
    private float m_pointRadius = 3f;
    private float m_dragSensitivity = DefaultDragSensitivity;
    private Vector2 m_dataMin;
    private Vector2 m_dataMax = Vector2.one;

    public IReadOnlyList<Vector2> DataPoints => m_dataPoints;
    public IReadOnlyList<GraphPointComponent> PointChildren => m_pointChildren;

    public float DragSensitivity
    {
        get => m_dragSensitivity;
        set => m_dragSensitivity = Math.Max(0.001f, value);
    }

    public event Action<int, Vector2>? OnPointDragged;
    public event Action<int, Vector2>? OnPointClicked;

    protected override void Init()
    {
        base.Init();
        m_meshFilter = gameObject.AddComponent<MeshFilter>();
        m_meshRenderer = gameObject.AddComponent<MeshRenderer>();
        m_mesh = new Mesh { name = "LineGraph" };
        m_mesh.MarkDynamic();
        m_meshFilter.mesh = m_mesh;

        var mat = new Material(Shader.Find("UI/Default"));
        m_meshRenderer.material = mat;
    }

    public LineGraphComponent WithPoints(IEnumerable<Vector2> points)
    {
        m_dataPoints.Clear();
        m_dataPoints.AddRange(points);
        return this;
    }

    public LineGraphComponent WithLineColor(Color color)
    {
        m_lineColor = color;
        return this;
    }

    public LineGraphComponent WithLineWidth(float width)
    {
        m_lineWidth = width;
        return this;
    }

    public LineGraphComponent WithPointRadius(float radius)
    {
        m_pointRadius = radius;
        return this;
    }

    public LineGraphComponent WithDragSensitivity(float sensitivity)
    {
        m_dragSensitivity = Math.Max(0.001f, sensitivity);
        return this;
    }

    public LineGraphComponent WithDataBounds(Vector2 min, Vector2 max)
    {
        m_dataMin = min;
        m_dataMax = max;
        return this;
    }

    public void SetPoints(IEnumerable<Vector2> points)
    {
        m_dataPoints.Clear();
        m_dataPoints.AddRange(points);
        Rebuild();
    }

    public void Rebuild()
    {
        RebuildPointChildren();
        RebuildLineMesh();
    }

    public Vector2 DataToLocal(Vector2 data)
    {
        var size = RectTransform.rect.size;
        var halfW = size.x * 0.5f;
        var halfH = size.y * 0.5f;
        var dataRange = m_dataMax - m_dataMin;
        if (dataRange.x <= 0f) dataRange.x = 1f;
        if (dataRange.y <= 0f) dataRange.y = 1f;

        return new Vector2(
            (data.x - m_dataMin.x) / dataRange.x * size.x - halfW,
            (data.y - m_dataMin.y) / dataRange.y * size.y - halfH
        );
    }

    public Vector2 LocalToData(Vector2 local)
    {
        var size = RectTransform.rect.size;
        var halfW = size.x * 0.5f;
        var halfH = size.y * 0.5f;
        var dataRange = m_dataMax - m_dataMin;

        return new Vector2(
            (local.x + halfW) / size.x * dataRange.x + m_dataMin.x,
            (local.y + halfH) / size.y * dataRange.y + m_dataMin.y
        );
    }

    private void RebuildPointChildren()
    {
        foreach (var child in m_pointChildren)
        {
            if (child != null) Destroy(child.gameObject);
        }
        m_pointChildren.Clear();

        for (int i = 0; i < m_dataPoints.Count; i++)
        {
            var pointChild = gameObject.AddInitChild<GraphPointComponent>();
            pointChild.Initialize(this, i, m_dataPoints[i], m_pointRadius, m_lineColor);
            int capturedIndex = i;
            pointChild.OnClick += () => OnPointClicked?.Invoke(capturedIndex, m_dataPoints[capturedIndex]);
            m_pointChildren.Add(pointChild);
        }
    }

    private void RebuildLineMesh()
    {
        if (m_mesh == null) return;
        m_mesh.Clear();

        int pointCount = m_pointChildren.Count;
        if (pointCount < 2) return;

        var vh = new VertexHelper();
        var lineColor32 = (Color32)m_lineColor;
        var halfWidth = m_lineWidth * 0.5f;
        int baseIdx = 0;

        var positions = new Vector2[pointCount];
        for (int i = 0; i < pointCount; i++)
            positions[i] = DataToLocal(m_dataPoints[i]);

        for (int i = 0; i < positions.Length - 1; i++)
        {
            var a = positions[i];
            var b = positions[i + 1];

            var segment = b - a;
            var tangent = segment.normalized;
            var bitangent = new Vector2(-tangent.y, tangent.x);

            var aLeft = a + bitangent * halfWidth;
            var aRight = a - bitangent * halfWidth;
            var bLeft = b + bitangent * halfWidth;
            var bRight = b - bitangent * halfWidth;

            var u0 = (float)i / (positions.Length - 1);
            var u1 = (float)(i + 1) / (positions.Length - 1);

            vh.AddVert(new Vector3(aLeft.x, aLeft.y, 0f), lineColor32, new Vector2(u0, 0f));
            vh.AddVert(new Vector3(aRight.x, aRight.y, 0f), lineColor32, new Vector2(u0, 1f));
            vh.AddVert(new Vector3(bRight.x, bRight.y, 0f), lineColor32, new Vector2(u1, 1f));
            vh.AddVert(new Vector3(bLeft.x, bLeft.y, 0f), lineColor32, new Vector2(u1, 0f));

            vh.AddTriangle(baseIdx, baseIdx + 1, baseIdx + 2);
            vh.AddTriangle(baseIdx, baseIdx + 2, baseIdx + 3);
            baseIdx += 4;
        }

        vh.FillMesh(m_mesh);
        m_mesh.RecalculateBounds();
        vh.Dispose();
    }

    internal void HandlePointDrag(int index, Vector2 newDataPosition)
    {
        newDataPosition.x = Mathf.Clamp(newDataPosition.x, m_dataMin.x, m_dataMax.x);
        newDataPosition.y = Mathf.Clamp(newDataPosition.y, m_dataMin.y, m_dataMax.y);

        m_dataPoints[index] = newDataPosition;

        if (index < m_pointChildren.Count && m_pointChildren[index] != null)
            m_pointChildren[index].UpdatePosition();

        RebuildLineMesh();
        OnPointDragged?.Invoke(index, newDataPosition);
    }

    private void OnDestroy()
    {
        if (m_mesh != null)
        {
            if (m_meshFilter != null) m_meshFilter.sharedMesh = null;
            DestroyImmediate(m_mesh);
        }
        if (m_meshRenderer != null && m_meshRenderer.sharedMaterial != null)
            DestroyImmediate(m_meshRenderer.sharedMaterial);
    }
}

public class GraphPointComponent : UIComponent, IPointerDownHandler, IPointerUpHandler
{
    internal const float DragDeadZoneDegrees = 2f;

    private LineGraphComponent? m_parentGraph;
    private int m_pointIndex;
    private ImageView? m_imageView;

    private bool m_isDragging;
    private bool m_dragActive;
    private bool m_wasDragged;
    private Transform? m_dragControllerTransform;
    private Vector3 m_dragStartForward;
    private Vector2 m_dragStartDataPosition;

    public int PointIndex => m_pointIndex;
    public Vector2 DataPosition => m_parentGraph?.DataPoints[m_pointIndex] ?? Vector2.zero;

    public event Action? OnClick;

    internal void Initialize(LineGraphComponent parent, int index, Vector2 dataPosition, float radius, Color color)
    {
        m_parentGraph = parent;
        m_pointIndex = index;

        RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        RectTransform.sizeDelta = new Vector2(radius * 2f, radius * 2f);

        m_imageView = gameObject.RequireComponent<ImageView>();
        m_imageView.raycastTarget = true;
        m_imageView.color = color;
        m_imageView.sprite = UIResources.LoadSpriteFromResource("VainSabers.ui_round.png", borderRatio: 0.5f);
        m_imageView.material = UIResources.NoGlowMat;

        UpdatePosition();
    }

    public void UpdatePosition()
    {
        if (m_parentGraph == null) return;
        var localPos = m_parentGraph.DataToLocal(m_parentGraph.DataPoints[m_pointIndex]);
        RectTransform.anchoredPosition = localPos;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        var controller = VRPointerManager.Instance?.ActiveTransform;
        if (controller == null || m_parentGraph == null) return;

        m_isDragging = true;
        m_dragActive = false;
        m_wasDragged = false;
        m_dragControllerTransform = controller;
        m_dragStartForward = controller.forward;
        m_dragStartDataPosition = m_parentGraph.DataPoints[m_pointIndex];
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        bool wasDrag = m_wasDragged;
        m_isDragging = false;
        m_dragActive = false;
        m_dragControllerTransform = null;

        if (!wasDrag)
            OnClick?.Invoke();
    }

    private void Update()
    {
        if (!m_isDragging || m_dragControllerTransform == null || m_parentGraph == null)
            return;

        var currentForward = m_dragControllerTransform.forward;

        var startForwardXZ = Vector3.ProjectOnPlane(m_dragStartForward, Vector3.up).normalized;
        var currentForwardXZ = Vector3.ProjectOnPlane(currentForward, Vector3.up).normalized;
        float yawDelta = Vector3.SignedAngle(startForwardXZ, currentForwardXZ, Vector3.up);

        var crossAxis = Vector3.Cross(m_dragStartForward, Vector3.up).normalized;
        var startInPlane = Vector3.ProjectOnPlane(m_dragStartForward, crossAxis).normalized;
        var currentInPlane = Vector3.ProjectOnPlane(currentForward, crossAxis).normalized;
        float pitchDelta = Vector3.SignedAngle(startInPlane, currentInPlane, crossAxis);

        float totalDelta = Mathf.Sqrt(yawDelta * yawDelta + pitchDelta * pitchDelta);

        if (!m_dragActive)
        {
            if (totalDelta > DragDeadZoneDegrees)
            {
                m_dragActive = true;
                m_wasDragged = true;
            }
        }

        if (!m_dragActive) return;

        float dataX = m_dragStartDataPosition.x + yawDelta * m_parentGraph.DragSensitivity;
        float dataY = m_dragStartDataPosition.y + pitchDelta * m_parentGraph.DragSensitivity;

        m_parentGraph.HandlePointDrag(m_pointIndex, new Vector2(dataX, dataY));
    }
}
