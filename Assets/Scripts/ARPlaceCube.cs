using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARPlaceCube : MonoBehaviour
{
    [SerializeField] private ARRaycastManager raycastManager;
    bool isPlacing = false;

    // 👇 STORE LAST OBJECT
    private GameObject lastSpawnedObject;

    void Start()
    {
        if (raycastManager == null)
        {
            Debug.LogError("[ARPlaceCube] ❌ ARRaycastManager is NOT assigned!");
        }
        else
        {
            Debug.Log("[ARPlaceCube] ✔ ARRaycastManager found.");
        }
    }

    void Update()
    {
        if (!raycastManager)
        {
            Debug.LogError("[ARPlaceCube] ❌ RaycastManager missing. Cannot place object.");
            return;
        }

        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
                Debug.Log("[ARPlaceCube] 📱 Touch detected at " + t.position);
        }

        if ((Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began ||
            Input.GetMouseButtonDown(0)) && !isPlacing)
        {
            isPlacing = true;

            Debug.Log("[ARPlaceCube] 🟦 Start placement attempt...");

            if (Input.touchCount > 0)
                PlaceObject(Input.GetTouch(0).position);
            else
                PlaceObject(Input.mousePosition);
        }
    }

    void PlaceObject(Vector2 touchPosition)
    {
        Debug.Log("[ARPlaceCube] 🔍 Raycasting at: " + touchPosition);

        var rayHits = new List<ARRaycastHit>();
        bool hitSomething = raycastManager.Raycast(touchPosition, rayHits, TrackableType.AllTypes);

        Debug.Log("[ARPlaceCube] 🎯 Raycast hit result = " + hitSomething);

        if (hitSomething && rayHits.Count > 0)
        {
            Pose hitPose = rayHits[0].pose;

            Debug.Log("[ARPlaceCube] ✅ HIT detected!");

            if (raycastManager.raycastPrefab != null)
            {
                // 👇 REMOVE PREVIOUS OBJECT
                if (lastSpawnedObject != null)
                {
                    Destroy(lastSpawnedObject);
                    Debug.Log("[ARPlaceCube] 🗑 Removed previous object");
                }

                // 👇 SPAWN NEW OBJECT
                lastSpawnedObject = Instantiate(
                    raycastManager.raycastPrefab,
                    hitPose.position,
                    hitPose.rotation
                );

                Debug.Log("[ARPlaceCube] ✔ Instantiated NEW object");
            }
            else
            {
                Debug.LogError("[ARPlaceCube] ❌ raycastPrefab on ARRaycastManager is NULL!");
            }
        }
        else
        {
            Debug.LogWarning("[ARPlaceCube] ⚠ No hit detected on any AR plane or trackable.");
        }

        StartCoroutine(SetIsPlacingToFalseWithDelay());
    }

    IEnumerator SetIsPlacingToFalseWithDelay()
    {
        Debug.Log("[ARPlaceCube] ⏳ Resetting placement lock...");
        yield return new WaitForSeconds(0.25f);
        isPlacing = false;
        Debug.Log("[ARPlaceCube] 🔄 Placement ready again.");
    }
}
