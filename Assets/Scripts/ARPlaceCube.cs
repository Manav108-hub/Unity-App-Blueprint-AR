using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARPlaceCube : MonoBehaviour
{
    [SerializeField] private ARRaycastManager raycastManager;
    private bool isPlacing = false;
    private GameObject lastSpawnedObject;

    void Start()
    {
        if (raycastManager == null) Debug.LogError("[ARPlaceCube] ARRaycastManager not assigned!");
        else Debug.Log("[ARPlaceCube] ARRaycastManager assigned.");
    }

    void Update()
    {
        if (!raycastManager) return;

        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                Debug.Log("[ARPlaceCube] Touch began at " + t.position);
                TryPlace(t.position);
            }
        }
        else if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("[ARPlaceCube] Mouse click at " + Input.mousePosition);
            TryPlace(Input.mousePosition);
        }
    }

    void TryPlace(Vector2 pos)
    {
        if (isPlacing) return;
        isPlacing = true;
        StartCoroutine(PlaceRoutine(pos));
    }

    IEnumerator PlaceRoutine(Vector2 pos)
    {
        Debug.Log("[ARPlaceCube] Raycasting at " + pos);
        var hits = new List<ARRaycastHit>();
        bool hit = raycastManager.Raycast(pos, hits, TrackableType.Planes);

        Debug.Log("[ARPlaceCube] Raycast hit? " + hit + " count=" + hits.Count);

        if (hit && hits.Count > 0)
        {
            var pose = hits[0].pose;
            Debug.Log("[ARPlaceCube] Hit pose: " + pose.position);

            // Remove previous
            if (lastSpawnedObject != null)
            {
                Destroy(lastSpawnedObject);
                Debug.Log("[ARPlaceCube] Removed previous placed object.");
            }

            if (raycastManager.raycastPrefab != null)
            {
                lastSpawnedObject = Instantiate(raycastManager.raycastPrefab, pose.position, pose.rotation);
                Debug.Log("[ARPlaceCube] Instantiated prefab as placed object.");
            }
            else
            {
                Debug.LogError("[ARPlaceCube] raycastPrefab is NULL on ARRaycastManager.");
            }
        }
        else
        {
            Debug.LogWarning("[ARPlaceCube] No plane hit; nothing instantiated.");
        }

        yield return new WaitForSeconds(0.25f);
        isPlacing = false;
    }
}
