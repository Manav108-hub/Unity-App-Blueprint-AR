using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

public class UIController : MonoBehaviour
{
    public RawImage previewImage;
    public GameObject previewPanel;
    public ARPlaneManager planeManager;
    public GameObject arUIGroup;

    void Start()
    {
        if (planeManager != null) planeManager.enabled = false;
        if (arUIGroup != null) arUIGroup.SetActive(false);
        if (previewImage != null) previewImage.gameObject.SetActive(false);
    }

    public void ShowPreviewImage(Texture2D tex)
    {
        if (previewImage == null) return;
        previewImage.texture = tex;
        previewImage.color = Color.white;
        previewImage.gameObject.SetActive(true);
        if (previewPanel != null) previewPanel.SetActive(true);
    }

    public void EnterARMode()
    {
        if (planeManager != null) planeManager.enabled = true;
        if (arUIGroup != null) arUIGroup.SetActive(true);
        if (previewPanel != null) previewPanel.SetActive(false);
    }

    public void ExitARMode()
    {
        if (planeManager != null) planeManager.enabled = false;
        if (arUIGroup != null) arUIGroup.SetActive(false);
    }
}
