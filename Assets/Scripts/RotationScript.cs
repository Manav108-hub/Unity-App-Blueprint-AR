using UnityEngine;
using UnityEngine.UI;

public class RotationScript : MonoBehaviour
{
    private Slider rotationSlider;
    public bool smoothRotation = true;
    public float smoothSpeed = 8f;

    private float targetZ = 0f;

    void Start()
    {
        // Find slider by name anywhere in the scene
        rotationSlider = GameObject.Find("RotateModel")?.GetComponent<Slider>();

        if (rotationSlider == null)
            Debug.LogError("[RotationScript] Slider named 'RotateModel' NOT found in scene!");
    }

    void Update()
    {
        if (rotationSlider == null) return;

        // Read slider value
        targetZ = rotationSlider.value;

        if (smoothRotation)
        {
            float newZ = Mathf.LerpAngle(transform.eulerAngles.z, targetZ, Time.deltaTime * smoothSpeed);
            transform.rotation = Quaternion.Euler(0, newZ, 0);
        }
        else
        {
            transform.rotation = Quaternion.Euler(0, targetZ, 0);
        }
    }
}
