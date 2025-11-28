using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using GLTFast;

public class SimpleARController : MonoBehaviour
{
    public static string glbPath;
    public static bool IsLocked = false;

    [Header("AR")]
    public ARRaycastManager raycastManager;
    public GameObject placementIndicator;

    private List<ARRaycastHit> hits = new List<ARRaycastHit>();
    private GameObject spawnedModel;
    private bool isLoading = false;

    private byte[] pendingGlbBytes = null;

    [Header("Scale / Placement")]
    public bool autoPlaceWhenReady = true;
    public Vector3 defaultLocalScale = Vector3.one * 0.2f;

    // ======================================================================
    //                               UI CHECK
    // ======================================================================
    bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;

        if (Input.touchCount > 0)
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);

        return EventSystem.current.IsPointerOverGameObject();
    }

    // ======================================================================
    //                      CALLED BY BackendConnector
    // ======================================================================
    public void SetGLBBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return;

        pendingGlbBytes = bytes;

        if (!IsLocked &&
            autoPlaceWhenReady &&
            placementIndicator != null &&
            placementIndicator.activeInHierarchy)
        {
            Pose p = new Pose(placementIndicator.transform.position,
                              placementIndicator.transform.rotation);

            LoadFromBytesAtPose(p);
            pendingGlbBytes = null;
        }
    }

    public void LoadFromBytesAtPose(Pose pose)
    {
        if (pendingGlbBytes == null) return;

        StartCoroutine(LoadFromBytesCoroutine(pendingGlbBytes, pose));
        pendingGlbBytes = null;
    }

    // ======================================================================
    //                               UPDATE
    // ======================================================================
    void Update()
    {
        // ============================================================
        // 1. IF LOCKED → STOP MOVEMENT & PLACEMENT
        // ============================================================
        if (IsLocked)
        {
            if (placementIndicator != null)
                placementIndicator.SetActive(false);
            return;
        }

        // ============================================================
        // 2. BLOCK PLACEMENT WHEN TOUCHING UI
        // ============================================================
        if (IsPointerOverUI())
            return;

        // ============================================================
        // 3. UPDATE PLACEMENT INDICATOR
        // ============================================================
        if (placementIndicator != null)
        {
            if (raycastManager.Raycast(new Vector2(Screen.width / 2f, Screen.height / 2f),
                                       hits, TrackableType.Planes))
            {
                placementIndicator.SetActive(true);
                placementIndicator.transform.position = hits[0].pose.position;
                placementIndicator.transform.rotation = hits[0].pose.rotation;
            }
            else
                placementIndicator.SetActive(false);
        }

        // ============================================================
        // 4. TOUCH → SPAWN GLB (ONLY WHEN NOT LOCKED)
        // ============================================================
        if (spawnedModel == null &&
            Input.touchCount > 0 &&
            Input.GetTouch(0).phase == TouchPhase.Began &&
            !isLoading)
        {
            if (raycastManager.Raycast(Input.GetTouch(0).position,
                                       hits, TrackableType.Planes))
            {
                Pose p = hits[0].pose;

                if (pendingGlbBytes != null)
                {
                    StartCoroutine(LoadFromBytesCoroutine(pendingGlbBytes, p));
                    pendingGlbBytes = null;
                }
                else if (!string.IsNullOrEmpty(glbPath) &&
                         File.Exists(glbPath))
                {
                    StartCoroutine(LoadFromFileCoroutine(glbPath, p));
                }
            }
        }
    }

    // ======================================================================
    //                           LOAD GLB FROM BYTES
    // ======================================================================
    IEnumerator LoadFromBytesCoroutine(byte[] bytes, Pose placePose)
    {
        if (isLoading) yield break;
        isLoading = true;

        LoaderController.Instance.Show();

        var gltf = new GltfImport();
        Task<bool> loadTask = gltf.LoadGltfBinary(bytes);
        yield return new WaitUntil(() => loadTask.IsCompleted);

        if (!loadTask.Result)
        {
            LoaderController.Instance.Hide();
            isLoading = false;
            yield break;
        }

        if (spawnedModel != null) Destroy(spawnedModel);
        spawnedModel = new GameObject("SpawnedModel");

        Task instTask = gltf.InstantiateMainSceneAsync(spawnedModel.transform);
        yield return new WaitUntil(() => instTask.IsCompleted);

        spawnedModel.transform.position = placePose.position;
        spawnedModel.transform.rotation = placePose.rotation;

        LoaderController.Instance.Hide();
        isLoading = false;
    }

    // ======================================================================
    //                           LOAD GLB FROM FILE
    // ======================================================================
    IEnumerator LoadFromFileCoroutine(string filePath, Pose placePose)
    {
        if (isLoading) yield break;
        isLoading = true;

        LoaderController.Instance.Show();

        var gltf = new GltfImport();
        Task<bool> loadTask = gltf.Load("file://" + filePath);
        yield return new WaitUntil(() => loadTask.IsCompleted);

        if (!loadTask.Result)
        {
            LoaderController.Instance.Hide();
            isLoading = false;
            yield break;
        }

        if (spawnedModel != null) Destroy(spawnedModel);
        spawnedModel = new GameObject("SpawnedModel");

        Task instTask = gltf.InstantiateMainSceneAsync(spawnedModel.transform);
        yield return new WaitUntil(() => instTask.IsCompleted);

        spawnedModel.transform.position = placePose.position;
        spawnedModel.transform.rotation = placePose.rotation;

        LoaderController.Instance.Hide();
        isLoading = false;
    }

    // ======================================================================
    //                               LOCK BUTTON
    // ======================================================================
    public void OnLockButton()
    {
        IsLocked = !IsLocked;
        Debug.Log("[SimpleARController] Locked = " + IsLocked);
    }
}
