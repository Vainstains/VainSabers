using System;
using UnityEngine;
using UnityEngine.EventSystems;
using VainSabers.Menu;

namespace VainSabers.UI
{
    /// <summary>
    /// Common reusable rotation-based drag handling for VR.
    /// Mirrors the logic previously duplicated in NumberInputComponent and GradientInputComponent.DiamondMarker.
    /// Uses controller yaw (projected forward onto XZ plane) with dead zone, no camera/screen conversion needed.
    /// </summary>
    public class ControllerYawDragHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public float DeadZoneDegrees = 2f;

        /// <summary>Effective angle in degrees since drag start, after dead zone subtraction.</summary>
        public event Action<float>? OnYawDragged;
        public event Action? OnDragStarted;
        public event Action? OnDragEnded;

        public bool IsDragging { get; private set; }
        public bool DragActive { get; private set; }
        public bool WasDragged { get; private set; }

        private Transform? m_dragControllerTransform;
        private Vector3 m_dragStartForwardXZ;
        private float m_deadZoneOffset;

        public void OnPointerDown(PointerEventData eventData)
        {
            var controller = VRPointerManager.Instance?.ActiveTransform;
            if (controller == null) return;

            IsDragging = true;
            DragActive = false;
            WasDragged = false;
            m_dragControllerTransform = controller;
            m_dragStartForwardXZ = Vector3.ProjectOnPlane(controller.forward, Vector3.up).normalized;
            if (m_dragStartForwardXZ.sqrMagnitude < 0.001f)
                m_dragStartForwardXZ = Vector3.forward;
            m_deadZoneOffset = 0f;
            OnDragStarted?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            bool wasActive = DragActive;
            IsDragging = false;
            DragActive = false;
            m_dragControllerTransform = null;
            if (wasActive)
                OnDragEnded?.Invoke();
        }

        private void Update()
        {
            if (!IsDragging || m_dragControllerTransform == null) return;

            Vector3 currentForwardXZ = Vector3.ProjectOnPlane(m_dragControllerTransform.forward, Vector3.up).normalized;
            if (currentForwardXZ.sqrMagnitude < 0.001f) return;

            float angle = Vector3.SignedAngle(m_dragStartForwardXZ, currentForwardXZ, Vector3.up);

            if (!DragActive)
            {
                if (Mathf.Abs(angle) > DeadZoneDegrees)
                {
                    DragActive = true;
                    WasDragged = true;
                    m_deadZoneOffset = angle;
                }
                else
                {
                    return;
                }
            }

            float effectiveAngle = angle - m_deadZoneOffset;
            OnYawDragged?.Invoke(effectiveAngle);
        }

        public void ResetDrag()
        {
            IsDragging = false;
            DragActive = false;
            WasDragged = false;
            m_dragControllerTransform = null;
        }
    }
}
