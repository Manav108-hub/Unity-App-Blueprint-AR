using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.Android;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class BackendConnector : MonoBehaviour
{
    public string uploadUrl = "http://16.171.206.252:5000/image-to-glb";
    public int maxUploadBytes = 6 * 1024 * 1024;
    public int jpegQuality = 80;

    public static byte[] lastGlbBytes;
    public static List<byte[]> glbHistory = new();
    public static event System.Action<byte[]> OnNewGLB;

    [SerializeField] private GLBLoader loader;
    [SerializeField] private ARRaycastManager raycastManager;

    void Awake()
    {
#if UNITY_EDITOR
        uploadUrl = "http://localhost:5000/image-to-glb";
#else
        uploadUrl = "http://16.171.206.252:5000/image-to-glb";
#endif

        if (!raycastManager)
            Debug.LogError("❌ ARRaycastManager not assigned!");
    }

    // ========== PICK IMAGE ==========
    public void OnUploadButton()
    {
        StartCoroutine(PickAndUpload());
    }

    IEnumerator PickAndUpload()
    {
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageRead))
        {
            Permission.RequestUserPermission(Permission.ExternalStorageRead);
            yield return null;
        }
#endif

        NativeGallery.GetImageFromGallery((path) =>
        {
            if (string.IsNullOrEmpty(path))
                return;

            StartCoroutine(UploadToServer(path));

        }, "Select image", "image/*");

        yield return null;
    }

    // ========== UPLOAD IMAGE + RECEIVE GLB ==========
    IEnumerator UploadToServer(string imagePath)
    {
        // 🔥 Show loader when upload starts
        LoaderController.Instance.Show();

        if (!File.Exists(imagePath))
        {
            Debug.LogError("❌ Image does not exist.");
            LoaderController.Instance.Hide();
            yield break;
        }

        byte[] imageBytes = File.ReadAllBytes(imagePath);

        byte[] uploadBytes = imageBytes;

        // Compression for large images
        if (imageBytes.Length > maxUploadBytes)
        {
            Debug.Log("Compressing oversized image...");

            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(imageBytes);

            float ratio = Mathf.Sqrt((float)maxUploadBytes / (float)imageBytes.Length);
            int w = Mathf.RoundToInt(tex.width * ratio);
            int h = Mathf.RoundToInt(tex.height * ratio);

            Texture2D scaled = ScaleTexture(tex, w, h);
            uploadBytes = scaled.EncodeToJPG(jpegQuality);

            DestroyImmediate(tex);
            DestroyImmediate(scaled);
        }

        List<IMultipartFormSection> form = new()
        {
            new MultipartFormFileSection("image", uploadBytes, Path.GetFileName(imagePath), "image/jpeg")
        };

        UnityWebRequest req = UnityWebRequest.Post(uploadUrl, form);
        req.downloadHandler = new DownloadHandlerBuffer();

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("❌ Upload failed: " + req.error);
            LoaderController.Instance.Hide();
            yield break;
        }

        byte[] response = req.downloadHandler.data;

        // If server directly returns GLB
        if (LooksLikeGLB(response))
        {
            HandleGLBBytes(response);
            yield break;
        }

        // JSON fallback
        string text = Encoding.UTF8.GetString(response);
        Debug.Log("Server text response: " + text);

        string glbUrl = null;

        if (text.StartsWith("{"))
        {
            int idx = text.IndexOf("glb_url");
            if (idx >= 0)
            {
                int q1 = text.IndexOf('"', idx + 7);
                int q2 = text.IndexOf('"', q1 + 1);
                glbUrl = text.Substring(q1 + 1, q2 - q1 - 1);
            }
        }

        if (!string.IsNullOrEmpty(glbUrl))
        {
            yield return StartCoroutine(DownloadGLB(glbUrl));
        }
        else
        {
            Debug.LogError("❌ No GLB URL found.");
            LoaderController.Instance.Hide();
        }
    }

    // ========== DOWNLOAD GLB ==========
    IEnumerator DownloadGLB(string url)
    {
        UnityWebRequest req = UnityWebRequest.Get(url);
        req.downloadHandler = new DownloadHandlerBuffer();

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("❌ Download failed: " + req.error);
            LoaderController.Instance.Hide();
            yield break;
        }

        HandleGLBBytes(req.downloadHandler.data);
    }

    // ========== CENTRAL GLB HANDLER ==========
    void HandleGLBBytes(byte[] glb)
    {
        if (glb == null || glb.Length == 0)
        {
            Debug.LogError("❌ Empty GLB!");
            LoaderController.Instance.Hide();
            return;
        }

        lastGlbBytes = glb;
        glbHistory.Add(glb);

        // Load as AR prefab safely
        StartCoroutine(loader.LoadGlbObject(glb, (model) =>
        {
            if (!model)
            {
                Debug.LogError("❌ GLB load failed (model null).");
                LoaderController.Instance.Hide();
                return;
            }

            raycastManager.raycastPrefab = model;
            Debug.Log("✅ Assigned GLB to AR prefab.");

            // 🔥 Hide loader ONLY AFTER Unity finishes loading the model
            LoaderController.Instance.Hide();
        }));

        // Fire preview listeners
        OnNewGLB?.Invoke(glb);
    }

    // ========== HELPERS ==========
    bool LooksLikeGLB(byte[] b)
    {
        return b != null && b.Length > 4 &&
               b[0] == (byte)'g' && b[1] == (byte)'l' &&
               b[2] == (byte)'T' && b[3] == (byte)'F';
    }

    Texture2D ScaleTexture(Texture2D src, int w, int h)
    {
        Texture2D dst = new Texture2D(w, h);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = src.GetPixel(
                    Mathf.FloorToInt((float)x / w * src.width),
                    Mathf.FloorToInt((float)y / h * src.height)
                );
                dst.SetPixel(x, y, c);
            }
        }

        dst.Apply();
        return dst;
    }
}
