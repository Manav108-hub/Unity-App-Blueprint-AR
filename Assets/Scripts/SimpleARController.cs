using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using GLTFast;

public class SimpleARController : MonoBehaviour
{
    public static string glbPath;                // legacy usage, optional
    public ARRaycastManager raycastManager;
    public GameObject placementIndicator;

    private List<ARRaycastHit> hits = new List<ARRaycastHit>();
    private GameObject spawnedModel;
    private bool isLocked = false;
    private bool isLoading = false;

    // store the bytes; loaded when backend provides them
    private byte[] pendingGlbBytes = null;

    [Header("Placement / Scale")]
    [Tooltip("If true, the controller will attempt to auto-place when bytes arrive (at center or placementIndicator).")]
    public bool autoPlaceWhenReady = true;

    [Tooltip("Default local scale for spawned model (tweak as required).")]
    public Vector3 defaultLocalScale = Vector3.one * 0.2f;

    // Called by BackendConnector when bytes are available
    public void SetGLBBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            Debug.LogError("[SimpleARController] SetGLBBytes received empty bytes");
            return;
        }
        pendingGlbBytes = bytes;
        Debug.Log($"[SimpleARController] Received GLB bytes ({bytes.Length} bytes)");

        // If auto place is on and placementIndicator already active, immediately place there.
        // Otherwise BackendConnector will call LoadFromBytesAtPose if it wants immediate placement.
        if (autoPlaceWhenReady && placementIndicator != null && placementIndicator.activeInHierarchy)
        {
            var pose = new Pose(placementIndicator.transform.position, placementIndicator.transform.rotation);
            LoadFromBytesAtPose(pose);
            pendingGlbBytes = null;
            Debug.Log("[SimpleARController] Auto-placed at placement indicator.");
        }
    }

    // Call this if you want to directly spawn at a pose (e.g., immediate placement)
    public void LoadFromBytesAtPose(Pose pose)
    {
        if (pendingGlbBytes == null)
        {
            Debug.LogWarning("[SimpleARController] No GLB bytes available to load.");
            return;
        }
        StartCoroutine(LoadFromBytesCoroutine(pendingGlbBytes, pose));
        pendingGlbBytes = null; // consume
    }

    // Typical user flow: touch a plane, then we load + place from pending bytes
    void Update()
    {
        // Update placement indicator based on centre raycast (optional)
        if (placementIndicator != null)
        {
            if (raycastManager != null &&
                raycastManager.Raycast(new Vector2(Screen.width / 2f, Screen.height / 2f), hits, TrackableType.Planes))
            {
                placementIndicator.SetActive(true);
                placementIndicator.transform.position = hits[0].pose.position;
                placementIndicator.transform.rotation = hits[0].pose.rotation;
            }
            else placementIndicator.SetActive(false);
        }

        if (isLocked) return;

        // Touch-to-place behavior
        if (spawnedModel == null && Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began && !isLoading)
        {
            if (raycastManager.Raycast(Input.GetTouch(0).position, hits, TrackableType.Planes))
            {
                Pose p = hits[0].pose;
                if (pendingGlbBytes != null && pendingGlbBytes.Length > 0)
                {
                    StartCoroutine(LoadFromBytesCoroutine(pendingGlbBytes, p));
                    pendingGlbBytes = null; // consume bytes (optional)
                }
                else if (!string.IsNullOrEmpty(glbPath) && File.Exists(glbPath))
                {
                    StartCoroutine(LoadFromFileCoroutine(glbPath, p));
                }
                else
                {
                    Debug.Log("[SimpleARController] No GLB available. Upload first.");
                }
            }
        }
    }

    IEnumerator LoadFromBytesCoroutine(byte[] bytes, Pose placePose)
    {
        if (isLoading) yield break;
        isLoading = true;

        Debug.Log("[SimpleARController] Loading GLB from bytes...");
        var gltf = new GltfImport();

        Task<bool> loadTask = null;
        try
        {
            loadTask = gltf.LoadGltfBinary(bytes);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[SimpleARController] Exception starting LoadGltfBinary: " + ex);
            isLoading = false;
            yield break;
        }

        yield return new WaitUntil(() => loadTask.IsCompleted);

        bool loaded = false;
        try
        {
            loaded = loadTask.Result;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[SimpleARController] Load task exception: " + ex);
            isLoading = false;
            yield break;
        }

        if (!loaded)
        {
            Debug.LogError("[SimpleARController] Failed to load GLB from bytes (gltf.Load returned false).");
            isLoading = false;
            yield break;
        }

        // destroy previous model if exists
        if (spawnedModel != null)
        {
            Destroy(spawnedModel);
            spawnedModel = null;
        }

        // instantiate into parent GameObject
        spawnedModel = new GameObject("SpawnedModel");
        Task instantiateTask = null;
        try
        {
            instantiateTask = gltf.InstantiateMainSceneAsync(spawnedModel.transform);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[SimpleARController] Exception starting InstantiateMainSceneAsync: " + ex);
            Destroy(spawnedModel);
            spawnedModel = null;
            isLoading = false;
            yield break;
        }

        yield return new WaitUntil(() => instantiateTask.IsCompleted);

        if (instantiateTask.IsFaulted)
        {
            Debug.LogError("[SimpleARController] Instantiate faulted: " + (instantiateTask.Exception != null ? instantiateTask.Exception.ToString() : "unknown"));
            Destroy(spawnedModel);
            spawnedModel = null;
            isLoading = false;
            yield break;
        }

        // Position & orient
        spawnedModel.transform.position = placePose.position;
        spawnedModel.transform.rotation = placePose.rotation;

        // Apply default scale (configurable)
        spawnedModel.transform.localScale = defaultLocalScale;

        Debug.Log("[SimpleARController] GLB loaded from bytes and instantiated.");
        isLoading = false;
    }

    IEnumerator LoadFromFileCoroutine(string filePath, Pose placePose)
    {
        if (isLoading) yield break;
        isLoading = true;

        Debug.Log("[SimpleARController] Loading GLB from file: " + filePath);
        var gltf = new GltfImport();
        Task<bool> loadTask = null;
        try
        {
            loadTask = gltf.Load("file://" + filePath);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[SimpleARController] Exception starting Load(file): " + ex);
            isLoading = false;
            yield break;
        }

        yield return new WaitUntil(() => loadTask.IsCompleted);

        bool loaded = false;
        try { loaded = loadTask.Result; }
        catch (System.Exception ex) { Debug.LogError("[SimpleARController] Load task exception: " + ex); }

        if (!loaded)
        {
            Debug.LogError("[SimpleARController] Failed to load GLB from file.");
            isLoading = false;
            yield break;
        }

        if (spawnedModel != null)
        {
            Destroy(spawnedModel);
            spawnedModel = null;
        }

        spawnedModel = new GameObject("SpawnedModel");
        Task instantiateTask = gltf.InstantiateMainSceneAsync(spawnedModel.transform);
        yield return new WaitUntil(() => instantiateTask.IsCompleted);

        if (instantiateTask.IsFaulted)
        {
            Debug.LogError("[SimpleARController] Instantiate faulted: " + (instantiateTask.Exception != null ? instantiateTask.Exception.ToString() : "unknown"));
            Destroy(spawnedModel);
            spawnedModel = null;
            isLoading = false;
            yield break;
        }

        spawnedModel.transform.position = placePose.position;
        spawnedModel.transform.rotation = placePose.rotation;
        spawnedModel.transform.localScale = defaultLocalScale;

        isLoading = false;
        Debug.Log("[SimpleARController] GLB loaded from file and instantiated.");
    }

    public void OnRotateButton()
    {
        if (spawnedModel != null)
            spawnedModel.transform.Rotate(Vector3.up, 30f, Space.Self);
    }

    public void OnLockButton()
    {
        isLocked = !isLocked;
        Debug.Log("[SimpleARController] Model " + (isLocked ? "locked" : "unlocked"));
    }
}
