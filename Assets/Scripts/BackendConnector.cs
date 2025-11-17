using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class BackendConnector : MonoBehaviour
{
    // Default URL (will be overridden in Awake)
    public string uploadUrl = "http://51.20.107.234:5000/image-to-glb";

    // Optional: max size after compression (bytes)
    public int maxUploadBytes = 6 * 1024 * 1024; // 6 MB

    // JPEG quality for compression (0–100)
    [Range(10, 95)]
    public int jpegQuality = 80;

    // -------------------------------------------------------
    // ✅ Fix: Always override URL depending on platform
    // -------------------------------------------------------
    void Awake()
    {
#if UNITY_EDITOR
        uploadUrl = "http://localhost:5000/image-to-glb";
        Debug.Log("[BackendConnector] Running in Editor → using LOCALHOST endpoint: " + uploadUrl);
#else
        uploadUrl = "http://51.20.107.234:5000/image-to-glb";
        Debug.Log("[BackendConnector] Running on Device → using SERVER endpoint: " + uploadUrl);
#endif
    }

    public void OnUploadButton()
    {
        StartCoroutine(PickAndUpload());
    }

    IEnumerator PickAndUpload()
    {
        Debug.Log("[BackendConnector] Opening gallery...");

#if UNITY_ANDROID
        // Make sure permission granted
        if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageRead))
        {
            Permission.RequestUserPermission(Permission.ExternalStorageRead);
            yield return null;
        }
#endif

        // Use NativeGallery callback
        NativeGallery.GetImageFromGallery((path) =>
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.Log("[BackendConnector] No image selected.");
                return;
            }

            Debug.Log("[BackendConnector] Selected image: " + path);
            StartCoroutine(UploadToServer(path));
        }, "Select an image", "image/*");

        yield return null;
    }

    IEnumerator UploadToServer(string imagePath)
    {
        if (!File.Exists(imagePath))
        {
            Debug.LogError("[BackendConnector] Selected image file does not exist: " + imagePath);
            yield break;
        }

        byte[] imageBytes = File.ReadAllBytes(imagePath);
        Debug.Log($"[BackendConnector] Picked image bytes: {imageBytes.Length}");

        byte[] uploadBytes = imageBytes;

        // Compress if needed
        if (imageBytes.Length > maxUploadBytes)
        {
            Debug.Log($"[BackendConnector] Image larger than {maxUploadBytes} bytes, compressing...");
            Texture2D tex = new Texture2D(2, 2);
            bool loaded = tex.LoadImage(imageBytes, markNonReadable: false);
            if (loaded)
            {
                float ratio = Mathf.Sqrt((float)maxUploadBytes / (float)imageBytes.Length);
                int newW = Mathf.Max(32, Mathf.RoundToInt(tex.width * ratio));
                int newH = Mathf.Max(32, Mathf.RoundToInt(tex.height * ratio));

                Texture2D scaled = ScaleTexture(tex, newW, newH);
                uploadBytes = scaled.EncodeToJPG(jpegQuality);
                Debug.Log($"[BackendConnector] After compress: {uploadBytes.Length} bytes ({newW}x{newH})");

                DestroyImmediate(tex);
                DestroyImmediate(scaled);
            }
            else
            {
                Debug.LogWarning("[BackendConnector] Failed to compress, sending original.");
            }
        }

        // Build form data — server expects field “image”
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>
        {
            new MultipartFormFileSection("image", uploadBytes, Path.GetFileName(imagePath), "image/jpeg")
        };

        UnityWebRequest uwr = UnityWebRequest.Post(uploadUrl, formData);
        uwr.timeout = 120;
        uwr.downloadHandler = new DownloadHandlerBuffer();

        Debug.Log($"[BackendConnector] Uploading → {uploadUrl}, file={Path.GetFileName(imagePath)}, size={uploadBytes.Length}");

        var operation = uwr.SendWebRequest();
        while (!operation.isDone)
        {
            Debug.Log($"[BackendConnector] Upload progress: {uwr.uploadProgress * 100f:0.0}%");
            yield return null;
        }

#if UNITY_2020_1_OR_NEWER
        if (uwr.result != UnityWebRequest.Result.Success)
#else
        if (uwr.isNetworkError || uwr.isHttpError)
#endif
        {
            Debug.LogError($"[BackendConnector] Upload failed → {uwr.error} | code: {uwr.responseCode}");
            yield break;
        }

        string contentType = uwr.GetResponseHeader("Content-Type") ?? "";
        Debug.Log($"[BackendConnector] Upload OK → code={uwr.responseCode}, content-type={contentType}");

        byte[] responseBytes = uwr.downloadHandler.data;
        string textResponse = null;

        if (responseBytes != null && responseBytes.Length > 0)
        {
            if (contentType.Contains("model/gltf-binary") || LooksLikeGLB(responseBytes))
            {
                Debug.Log($"[BackendConnector] Server returned GLB bytes directly ({responseBytes.Length})");
                HandleGLBBytes(responseBytes);
                yield break;
            }

            textResponse = Encoding.UTF8.GetString(responseBytes).Trim();
            Debug.Log("[BackendConnector] Upload response text: " + textResponse);
        }
        else
        {
            Debug.LogWarning("[BackendConnector] Empty upload response");
        }

        string glbUrl = null;
        if (!string.IsNullOrEmpty(textResponse))
        {
            if (textResponse.StartsWith("{"))
            {
                int idx = textResponse.IndexOf("glb_url");
                if (idx >= 0)
                {
                    int colon = textResponse.IndexOf(':', idx);
                    int firstQuote = textResponse.IndexOf('"', colon);
                    int secondQuote = textResponse.IndexOf('"', firstQuote + 1);
                    if (firstQuote >= 0 && secondQuote > firstQuote)
                        glbUrl = textResponse.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
                }
            }
            else if (textResponse.StartsWith("http"))
                glbUrl = textResponse;
        }

        if (!string.IsNullOrEmpty(glbUrl))
        {
            Debug.Log("[BackendConnector] Server returned GLB URL → " + glbUrl);
            yield return StartCoroutine(DownloadGLB(glbUrl));
        }
        else
        {
            Debug.LogError("[BackendConnector] Could not find GLB bytes or URL in response");
        }
    }

    IEnumerator DownloadGLB(string glbUrl)
    {
        Debug.Log("[BackendConnector] Downloading GLB → " + glbUrl);
        UnityWebRequest req = UnityWebRequest.Get(glbUrl);
        req.timeout = 120;
        req.downloadHandler = new DownloadHandlerBuffer();

        yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
        if (req.result != UnityWebRequest.Result.Success)
#else
        if (req.isNetworkError || req.isHttpError)
#endif
        {
            Debug.LogError("[BackendConnector] Download failed → " + req.error + " | code: " + req.responseCode);
            yield break;
        }

        byte[] glbBytes = req.downloadHandler.data;
        Debug.Log("[BackendConnector] Downloaded GLB bytes: " + (glbBytes?.Length ?? 0));
        HandleGLBBytes(glbBytes);
    }

    void HandleGLBBytes(byte[] glbBytes)
    {
        if (glbBytes == null || glbBytes.Length == 0)
        {
            Debug.LogError("[BackendConnector] Empty GLB bytes");
            return;
        }

        var ar = FindObjectOfType<SimpleARController>();
        if (ar != null)
        {
            ar.SetGLBBytes(glbBytes);
            Debug.Log("[BackendConnector] Passed GLB bytes to SimpleARController");
        }
        else
        {
            string path = Path.Combine(Application.persistentDataPath, "model.glb");
            File.WriteAllBytes(path, glbBytes);
            SimpleARController.glbPath = path;
            Debug.Log("[BackendConnector] Saved GLB to disk fallback → " + path);
        }
    }

    // call this when you have the original image bytes (before upload) or the server returns an image preview
    void ShowImagePreview(byte[] imageBytes)
    {
        if (imageBytes == null || imageBytes.Length == 0) return;
        Texture2D tex = new Texture2D(2, 2);
        bool ok = tex.LoadImage(imageBytes); // auto-resizes texture
        if (!ok) { Debug.LogWarning("Failed to create texture from bytes"); return; }

        var ui = FindObjectOfType<UIController>();
        if (ui != null) ui.ShowPreviewImage(tex);
    }


    bool LooksLikeGLB(byte[] b)
    {
        if (b == null || b.Length < 4) return false;
        return b[0] == (byte)'g' && b[1] == (byte)'l' && b[2] == (byte)'T' && b[3] == (byte)'F';
    }

    Texture2D ScaleTexture(Texture2D src, int width, int height)
    {
        Texture2D dst = new Texture2D(width, height, src.format, false);
        for (int y = 0; y < height; y++)
        {
            int yy = Mathf.Clamp(Mathf.RoundToInt((float)y / height * src.height), 0, src.height - 1);
            for (int x = 0; x < width; x++)
            {
                int xx = Mathf.Clamp(Mathf.RoundToInt((float)x / width * src.width), 0, src.width - 1);
                dst.SetPixel(x, y, src.GetPixel(xx, yy));
            }
        }
        dst.Apply();
        return dst;
    }
}
