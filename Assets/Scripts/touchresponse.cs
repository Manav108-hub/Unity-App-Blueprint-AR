using UnityEngine;

public class touchresponse : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 0.2f;     // how fast the object rotates
    public float damping = 5f;             // smoothness

    [Header("Zoom Settings")]
    public float zoomSpeed = 0.02f;        // pinch zoom sensitivity
    public float minScale = 0.1f;          // smallest allowed size
    public float maxScale = 5f;            // largest allowed size

    private Vector2 lastTouchPos;
    private Quaternion targetRotation;
    private Vector3 targetScale;

    void Start()
    {
        // Initial targets
        targetRotation = transform.rotation;
        targetScale = transform.localScale;
    }

    void Update()
    {
        HandleRotation();
        HandlePinchZoom();

        // Apply smoothing
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * damping);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * damping);
    }

    // ----------------------------------------------------------
    // ROTATION BY SWIPE
    // ----------------------------------------------------------
    void HandleRotation()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            // Start of swipe
            if (touch.phase == TouchPhase.Began)
            {
                lastTouchPos = touch.position;
            }
            else if (touch.phase == TouchPhase.Moved)
            {
                Vector2 delta = touch.position - lastTouchPos;
                lastTouchPos = touch.position;

                float rotX = delta.y * rotationSpeed; // vertical swipe rotates around X
                float rotY = -delta.x * rotationSpeed; // horizontal swipe rotates around Y

                // Apply rotation to target (smoother)
                targetRotation *= Quaternion.Euler(rotX, rotY, 0f);
            }
        }
    }

    // ----------------------------------------------------------
    // PINCH TO ZOOM
    // ----------------------------------------------------------
    void HandlePinchZoom()
    {
        if (Input.touchCount == 2)
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            // Current distance
            float currDist = (t0.position - t1.position).magnitude;

            // Previous distance
            float prevDist = ((t0.position - t0.deltaPosition) -
                              (t1.position - t1.deltaPosition)).magnitude;

            // Difference ↓ (positive = moving apart → zoom in)
            float diff = currDist - prevDist;

            // Apply scaling
            float scaleFactor = 1 + diff * zoomSpeed;

            targetScale *= scaleFactor;

            // Clamp scaling
            float uniform = Mathf.Clamp(targetScale.x, minScale, maxScale);
            targetScale = new Vector3(uniform, uniform, uniform);
        }
    }
}
