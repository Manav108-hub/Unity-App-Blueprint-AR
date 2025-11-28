using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using GLTFast;

public class previewGLB : MonoBehaviour
{
    [Tooltip("Scale applied to the previewed GLB.")]
    public Vector3 previewScale = Vector3.one * 0.2f;

    private GameObject previewInstance;

    // ------------------------------------------------------------
    // Subscribe to GLB update event
    // ------------------------------------------------------------
    void OnEnable()
    {
        BackendConnector.OnNewGLB += OnNewGlbReceived;
    }

    void OnDisable()
    {
        BackendConnector.OnNewGLB -= OnNewGlbReceived;
    }

    // ------------------------------------------------------------
    // Start: load last GLB
    // ------------------------------------------------------------
    IEnumerator Start()
    {
        yield return null;

        if (BackendConnector.lastGlbBytes != null &&
            BackendConnector.lastGlbBytes.Length > 0)
        {
            yield return StartCoroutine(LoadGLB(BackendConnector.lastGlbBytes));
        }
    }

    // ------------------------------------------------------------
    // EVENT — New GLB received
    // ------------------------------------------------------------
    void OnNewGlbReceived(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return;
        StartCoroutine(LoadGLB(bytes));
    }

    // ------------------------------------------------------------
    // Load GLB (GLTFast)
    // ------------------------------------------------------------
    IEnumerator LoadGLB(byte[] bytes)
    {
        var gltf = new GltfImport();
        Task<bool> loadTask = gltf.LoadGltfBinary(bytes);

        yield return new WaitUntil(() => loadTask.IsCompleted);

        if (!loadTask.Result)
        {
            Debug.LogError("GLB parsing FAILED.");
            yield break;
        }

        // Destroy old preview
        if (previewInstance != null)
            Destroy(previewInstance);

        previewInstance = new GameObject("GLB_Preview");
        previewInstance.transform.SetParent(transform, false);

        // Instantiate scene
        Task instantiateTask = gltf.InstantiateMainSceneAsync(previewInstance.transform);
        yield return new WaitUntil(() => instantiateTask.IsCompleted);

        if (instantiateTask.IsFaulted)
        {
            Debug.LogError("Instantiation FAILED: " + instantiateTask.Exception);
            Destroy(previewInstance);
            previewInstance = null;
            yield break;
        }

        // ------------------------------------------------------------
        // REMOVE ALL OBJECTS CONTAINING "ceiling"
        // ------------------------------------------------------------
        RemoveCeilingObjects(previewInstance.transform);

        // ------------------------------------------------------------
        // Apply final transforms
        // ------------------------------------------------------------
        previewInstance.transform.localPosition = Vector3.zero;
        previewInstance.transform.localScale = previewScale * 3f;

        previewInstance.transform.localRotation = Quaternion.Euler(
            338.878967f,
            46.7950897f,
            338.633179f
        );
    }

    // ============================================================
    // Remove instantiated children whose names contain "ceiling"
    // ============================================================
    void RemoveCeilingObjects(Transform root)
    {
        // Must copy children to list to avoid modifying collection while iterating
        List<Transform> childrenToCheck = new List<Transform>();

        foreach (Transform child in root)
            childrenToCheck.Add(child);

        foreach (Transform child in childrenToCheck)
        {
            if (child.name.ToLower().Contains("ceiling"))
            {
                Debug.Log("Removing CEILING object: " + child.name);
                Destroy(child.gameObject);
                continue;
            }

            // Recursively check deeper nodes
            RemoveCeilingObjects(child);
        }
    }
}
