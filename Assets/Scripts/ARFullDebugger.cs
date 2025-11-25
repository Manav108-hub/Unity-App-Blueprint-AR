using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class ARFullDebugger : MonoBehaviour
{
    [Header("Auto References")]
    public ARPlaneManager planeManager;
    public ARRaycastManager raycastManager;
    public ARCameraManager cameraManager;
    public ARCameraBackground cameraBackground;

    private List<ARRaycastHit> hits = new List<ARRaycastHit>();
    private bool planeLogPrinted = false;

    void Start()
    {
        // Auto-assign if not set manually
        if (planeManager == null) planeManager = GetComponent<ARPlaneManager>();
        if (raycastManager == null) raycastManager = GetComponent<ARRaycastManager>();
        if (cameraManager == null) cameraManager = FindObjectOfType<ARCameraManager>();
        if (cameraBackground == null) cameraBackground = FindObjectOfType<ARCameraBackground>();

        // Log components
        DebugComponent("ARPlaneManager", planeManager);
        DebugComponent("ARRaycastManager", raycastManager);
        DebugComponent("ARCameraManager", cameraManager);
        DebugComponent("ARCameraBackground", cameraBackground);

        // Subscribe to plane events
        if (planeManager != null)
            planeManager.planesChanged += OnPlanesChanged;

        // Subscribe to camera frame updates
        if (cameraManager != null)
            cameraManager.frameReceived += OnCameraFrameReceived;
    }

    void DebugComponent(string name, Object component)
    {
        if (component == null)
            Debug.LogError($"[ARFullDebugger] ❌ {name} NOT FOUND!");
        else
            Debug.Log($"[ARFullDebugger] ✔ {name} found.");
    }

    // ---------------------------------------------------
    // PLANE DETECTION DEBUG
    // ---------------------------------------------------
    void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        if (!planeLogPrinted)
        {
            Debug.Log("[ARFullDebugger] 🔍 Plane detection active.");
            planeLogPrinted = true;
        }

        if (args.added.Count > 0)
            Debug.Log("[ARFullDebugger] ➕ PLANES ADDED: " + args.added.Count);

        if (args.updated.Count > 0)
            Debug.Log("[ARFullDebugger] 🔄 PLANES UPDATED: " + args.updated.Count);
    }

    // ---------------------------------------------------
    // CAMERA FRAME DEBUG (Tracking)
    // ---------------------------------------------------
    void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        // This is valid for AR Foundation 4.2–5.x
        if (args.exposureDuration.HasValue || args.lightEstimation.averageBrightness.HasValue)
        {
            Debug.Log("[ARFullDebugger] 📡 Camera tracking active (frame received).");
        }
    }

    // ---------------------------------------------------
    // RAYCAST DEBUG (center screen)
    // ---------------------------------------------------
    void Update()
    {
        if (raycastManager == null) return;

        Vector2 center = new Vector2(Screen.width / 2f, Screen.height / 2f);

        if (raycastManager.Raycast(center, hits, TrackableType.Planes))
        {
            Debug.Log("[ARFullDebugger] 🎯 Raycast HIT at " + hits[0].pose.position);
        }
    }
}
