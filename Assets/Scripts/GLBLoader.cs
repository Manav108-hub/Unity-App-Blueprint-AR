using UnityEngine;
using System.Collections;
using GLTFast;

public class GLBLoader : MonoBehaviour
{
    public IEnumerator LoadGlbObject(byte[] bytes, System.Action<GameObject> callback)
    {
        var gltf = new GltfImport();

        var loadTask = gltf.LoadGltfBinary(bytes);
        yield return new WaitUntil(() => loadTask.IsCompleted);

        if (!loadTask.Result)
        {
            Debug.LogError("❌ GLB parsing FAILED.");
            callback(null);
            yield break;
        }

        Debug.Log("✅ GLB parsed successfully.");

        GameObject container = new GameObject("GLB_ROOT");

        var instTask = gltf.InstantiateMainSceneAsync(container.transform);
        yield return new WaitUntil(() => instTask.IsCompleted);

        if (instTask.IsFaulted)
        {
            Debug.LogError("❌ GLB instantiate FAILED");

            if (instTask.Exception != null)
                Debug.LogError("🔥 GLTFast INTERNAL EXCEPTION:\n" + instTask.Exception);

            Destroy(container);
            callback(null);
            yield break;
        }

        Debug.Log("✅ GLB instantiated successfully.");

        // *** DO NOT EXTRACT A SINGLE CHILD ***
        // The entire GLB hierarchy must remain intact.
        
        callback(container);
    }
}
