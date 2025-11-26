using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ARPlaceCube : MonoBehaviour
{
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private Slider scaleSlider;

    [Header("Long press settings")]
    public float longPressSeconds = 1.5f;
    public float maxMoveThresholdPx = 20f;

    private bool isPlacing = false;
    private GameObject lastSpawnedObject;

    // long-press tracking
    private bool trackingPress = false;
    private float pressStartTime = 0f;
    private Vector2 pressStartPos = Vector2.zero;
    private int trackingFingerId = -1;

    void Start()
    {
        if (raycastManager == null)
            Debug.LogError("[ARPlaceCube] ARRaycastManager not assigned!");

        if (scaleSlider == null)
            Debug.LogError("[ARPlaceCube] ScaleSlider NOT assigned!");
    }

    void Update()
    {
        if (!raycastManager) return;

        if (SimpleARController.IsLocked) return;

        if (IsPointerOverUI()) return;

#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseLongPress();
#else
        HandleTouchLongPress();
#endif
    }

    bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;

        if (Input.touchCount > 0)
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);

        return EventSystem.current.IsPointerOverGameObject();
    }

    void HandleTouchLongPress()
    {
        if (Input.touchCount == 0)
        {
            trackingPress = false;
            trackingFingerId = -1;
            return;
        }

        Touch t = Input.GetTouch(0);

        if (t.phase == TouchPhase.Began)
        {
            trackingPress = true;
            pressStartTime = Time.time;
            pressStartPos = t.position;
            trackingFingerId = t.fingerId;
        }
        else if (trackingPress && t.fingerId == trackingFingerId)
        {
            float moveDist = Vector2.Distance(t.position, pressStartPos);

            if (moveDist > maxMoveThresholdPx)
            {
                trackingPress = false;
                trackingFingerId = -1;
                return;
            }

            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                float held = Time.time - pressStartTime;

                trackingPress = false;
                trackingFingerId = -1;

                if (held >= longPressSeconds)
                {
                    TryPlace(t.position);
                }
            }
        }
    }

    private bool mouseTracking = false;
    private float mouseStartTime = 0f;
    private Vector2 mouseStartPos = Vector2.zero;

    void HandleMouseLongPress()
    {
        if (Input.GetMouseButtonDown(0))
        {
            mouseTracking = true;
            mouseStartTime = Time.time;
            mouseStartPos = Input.mousePosition;
        }
        else if (mouseTracking)
        {
            float moveDist = Vector2.Distance((Vector2)Input.mousePosition, mouseStartPos);

            if (moveDist > maxMoveThresholdPx)
            {
                mouseTracking = false;
                return;
            }

            if (Input.GetMouseButtonUp(0))
            {
                float held = Time.time - mouseStartTime;
                mouseTracking = false;

                if (held >= longPressSeconds)
                {
                    TryPlace(Input.mousePosition);
                }
            }
        }
    }

    void TryPlace(Vector2 screenPos)
    {
        if (isPlacing) return;

        if (SimpleARController.IsLocked) return;
        if (IsPointerOverUI()) return;

        isPlacing = true;
        StartCoroutine(PlaceRoutine(screenPos));
    }

    IEnumerator PlaceRoutine(Vector2 pos)
    {
        var hits = new List<ARRaycastHit>();
        bool hit = raycastManager.Raycast(pos, hits, TrackableType.Planes);

        if (hit && hits.Count > 0)
        {
            var pose = hits[0].pose;

            if (lastSpawnedObject != null)
            {
                Destroy(lastSpawnedObject);
                Debug.Log("[ARPlaceCube] Removed previous cube.");
            }

            if (raycastManager.raycastPrefab != null)
            {
                lastSpawnedObject = Instantiate(raycastManager.raycastPrefab, pose.position, pose.rotation);
                Debug.Log("[ARPlaceCube] Cube placed: " + lastSpawnedObject.name);

                float s = scaleSlider != null ? scaleSlider.value : 1f;
                if (s <= 0f) s = 0.0001f;

                lastSpawnedObject.transform.localScale = Vector3.one * s;

                Debug.Log($"[ARPlaceCube] Scale applied: {s}");

                // ⭐ ADD ROTATION SCRIPT HERE ⭐
                lastSpawnedObject.AddComponent<RotationScript>();
                Debug.Log("[ARPlaceCube] RotationScript attached to object.");
            }
        }

        yield return new WaitForSeconds(0.25f);
        isPlacing = false;
    }
}
