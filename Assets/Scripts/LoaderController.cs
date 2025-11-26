using UnityEngine;

public class LoaderController : MonoBehaviour
{
    public static LoaderController Instance;
    [SerializeField] GameObject loaderPanel;

    void Awake()
    {
        Instance = this;
        loaderPanel.SetActive(false);
    }

    public void Show() => loaderPanel.SetActive(true);
    public void Hide() => loaderPanel.SetActive(false);
}
