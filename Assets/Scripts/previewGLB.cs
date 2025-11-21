using UnityEngine;
using System.Collections;
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
        Debug.Log("[previewGLB] OnEnable() → Subscribing to BackendConnector.OnNewGLB");
        BackendConnector.OnNewGLB += OnNewGlbReceived;
    }

    void OnDisable()
    {
        Debug.Log("[previewGLB] OnDisable() → Unsubscribing from BackendConnector.OnNewGLB");
        BackendConnector.OnNewGLB -= OnNewGlbReceived;
    }

    // ------------------------------------------------------------
    // Start: load last GLB if already available
    // ------------------------------------------------------------
    IEnumerator Start()
    {
        Debug.Log("[previewGLB] Start() called → initializing preview loader.");

        // Allow BackendConnector to initialize first
        yield return null;

        if (BackendConnector.lastGlbBytes != null &&
            BackendConnector.lastGlbBytes.Length > 0)
        {
            Debug.Log("[previewGLB] Found previously loaded GLB → loading preview...");
            yield return StartCoroutine(LoadGLB(BackendConnector.lastGlbBytes));
        }
        else
        {
            Debug.Log("[previewGLB] No GLB available at Start(). Waiting for event...");
        }
    }

    // ------------------------------------------------------------
    // EVENT: Called whenever a new GLB arrives from the server
    // ------------------------------------------------------------
    void OnNewGlbReceived(byte[] bytes)
    {
        Debug.Log("[previewGLB] EVENT → New GLB received (" +
                  (bytes?.Length ?? 0) + " bytes). Reloading preview.");

        if (bytes == null || bytes.Length == 0)
        {
            Debug.LogWarning("[previewGLB] Received empty GLB in event callback.");
            return;
        }

        StartCoroutine(LoadGLB(bytes));
    }

    // ------------------------------------------------------------
    // Load GLB via GLTFast
    // ------------------------------------------------------------
    IEnumerator LoadGLB(byte[] bytes)
    {
        Debug.Log("[previewGLB] LoadGLB() → Starting GLTFast loading...");

        var gltf = new GltfImport();
        Task<bool> loadTask = null;

        try
        {
            Debug.Log("[previewGLB] Calling gltf.LoadGltfBinary()");
            loadTask = gltf.LoadGltfBinary(bytes);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[previewGLB] EXCEPTION during LoadGltfBinary: " + ex);
            yield break;
        }

        Debug.Log("[previewGLB] Waiting for GLB to finish parsing...");
        yield return new WaitUntil(() => loadTask.IsCompleted);

        if (!loadTask.Result)
        {
            Debug.LogError("[previewGLB] GLB parsing FAILED.");
            yield break;
        }

        Debug.Log("[previewGLB] GLB successfully parsed.");

        // -----------------------------------
        // Destroy any previous preview model
        // -----------------------------------
        if (previewInstance != null)
        {
            Debug.Log("[previewGLB] Destroying previous preview instance.");
            Destroy(previewInstance);
        }

        // -----------------------------------
        // Create new container for model
        // -----------------------------------
        Debug.Log("[previewGLB] Creating new preview container...");
        previewInstance = new GameObject("GLB_Preview");
        previewInstance.transform.SetParent(transform, worldPositionStays: false);

        // -----------------------------------
        // Instantiate scene
        // -----------------------------------
        Task instantiateTask = null;

        try
        {
            Debug.Log("[previewGLB] Calling InstantiateMainSceneAsync()");
            instantiateTask = gltf.InstantiateMainSceneAsync(previewInstance.transform);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[previewGLB] EXCEPTION during InstantiateMainSceneAsync: " + ex);
            Destroy(previewInstance);
            previewInstance = null;
            yield break;
        }

        Debug.Log("[previewGLB] Waiting for instantiation to complete...");
        yield return new WaitUntil(() => instantiateTask.IsCompleted);

        if (instantiateTask.IsFaulted)
        {
            Debug.LogError("[previewGLB] INSTANCIATION FAILED: " + instantiateTask.Exception);
            Destroy(previewInstance);
            previewInstance = null;
            yield break;
        }

        Debug.Log("[previewGLB] GLB instantiated successfully.");

        // -----------------------------------
        // Apply final transform
        // -----------------------------------
        previewInstance.transform.localPosition = Vector3.zero;

        previewInstance.transform.localRotation = Quaternion.Euler(
            338.878967f,
            46.7950897f,
            338.633179f
        );

        // Uniform ×3 scaling
        previewInstance.transform.localScale = previewScale * 3f;

        Debug.Log("[previewGLB] Preview model ready under: " + gameObject.name);
    }
}
