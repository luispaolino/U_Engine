using UnityEngine;

[DisallowMultipleComponent]
public class UMK3CameraController : MonoBehaviour
{
    [Header("Fighters")]
    [Tooltip("Your two character Transforms.")]
    public Transform fighterA;
    public Transform fighterB;

    [Header("Stage Limits")]
    [Tooltip("Left boundary of the stage.")]
    public float stageLeft = -10f;
    [Tooltip("Right boundary of the stage.")]
    public float stageRight = 10f;

    [Header("Vertical Follow")]
    [Tooltip("Extra world-units above the highest fighter.")]
    public float verticalPadding = 2.0f;
    [Tooltip("Vertical speed when camera rises.")]
    public float riseSpeed = 8.0f;
    [Tooltip("Vertical speed when camera falls.")]
    public float fallSpeed = 5.0f;
    [Tooltip("0 = no vertical follow; 1 = full follow.")]
    [Range(0f, 1f)] public float verticalFollowFactor = 1.0f;

    [Header("Shake (press Z)")]
    [Tooltip("Shake magnitude when you press Z.")]
    public float testShakeMagnitude = 0.35f;
    [Tooltip("Shake duration when you press Z.")]
    public float testShakeDuration = 0.18f;

    // ─── Internal state ────────────────────
    private Vector3 _anchor;        // the camera’s starting position (locked Y/Z)
    private Vector3 _shakeOffset;
    private float   _shakeTimer;
    private Camera  _cam;           // cached Camera component
    private float   _halfWidth;     // half the camera width (world units)

    private void Start()
    {
        if (fighterA == null || fighterB == null)
        {
            Debug.LogError("UMK3CameraController: assign fighterA and fighterB in the Inspector.");
            enabled = false;
            return;
        }

        // Cache the camera component
        _cam = GetComponent<Camera>();
        if (_cam == null)
        {
            Debug.LogError("UMK3CameraController must be attached to a Camera.");
            enabled = false;
            return;
        }

        // Capture the camera's starting Y and Z positions
        _anchor = transform.position;
        UpdateCameraHalfWidth();
    }

    private void Update()
    {
        // Trigger shake on Z
        if (Input.GetKeyDown(KeyCode.Z))
            _shakeTimer = testShakeDuration;

        // Update shake offset
        if (_shakeTimer > 0f)
        {
            _shakeTimer  -= Time.deltaTime;
            _shakeOffset  = (Vector3)Random.insideUnitCircle * testShakeMagnitude;
        }
        else
        {
            _shakeOffset = Vector3.zero;
        }
    }

// In UMK3CameraController.cs

private void LateUpdate()
{
    // 1) Horizontal: Center X between fighters
    float midX = (fighterA.position.x + fighterB.position.x) * 0.5f;
    midX = Mathf.Clamp(midX, stageLeft + _halfWidth, stageRight - _halfWidth);

    // 2) Vertical: Calculate the desired camera Y position
    float desiredCamY;
    float highestFighterY = Mathf.Max(fighterA.position.y, fighterB.position.y);

    // Determine the Y the camera would ideally be at.
    // If fighters are high, it's based on their height, otherwise, it's the anchor.
    if (highestFighterY + verticalPadding > _anchor.y && verticalFollowFactor > 0)
    {
        // Calculate the target based on the highest fighter, blended by follow factor
        float rawFollowY = highestFighterY + verticalPadding;
        desiredCamY = _anchor.y + (rawFollowY - _anchor.y) * verticalFollowFactor;
    }
    else
    {
        // Fighters are not high enough to warrant moving above anchor, or no vertical follow
        desiredCamY = _anchor.y;
    }

    // Now, smoothly move the camera's current Y towards this desiredCamY
    float currentCamY = transform.position.y;
    float newCamY;

    if (Mathf.Approximately(currentCamY, desiredCamY)) // Already at the target (or very close)
    {
        newCamY = desiredCamY;
    }
    else
    {
        // Determine speed based on whether we are moving up or down to the desired Y
        float speed = desiredCamY > currentCamY ? riseSpeed : fallSpeed;
        newCamY = Mathf.MoveTowards(currentCamY, desiredCamY, speed * Time.deltaTime);
    }

    // 3) Apply final position (clamped X, smooth Y, locked Z) + shake
    transform.position = new Vector3(midX, newCamY, _anchor.z) + _shakeOffset;
}

    /// <summary>
    /// Calculates half the camera width in world-units at the camera’s Z.
    /// </summary>
    private void UpdateCameraHalfWidth()
    {
        if (_cam.orthographic)
        {
            _halfWidth = _cam.orthographicSize * _cam.aspect;
        }
        else
        {
            float distance = Mathf.Abs(_cam.transform.position.z - fighterA.position.z);
            float halfHeight = Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance;
            _halfWidth = halfHeight * _cam.aspect;
        }
    }

    /// <summary>
    /// Public API to shake the camera.
    /// </summary>
    public void StartShake(float magnitude, float duration)
    {
        testShakeMagnitude = magnitude;
        testShakeDuration  = duration;
        _shakeTimer        = duration;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Visualize the stage limits
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(stageLeft, _anchor.y - 5f, _anchor.z), 
                        new Vector3(stageLeft, _anchor.y + 5f, _anchor.z));
        Gizmos.DrawLine(new Vector3(stageRight, _anchor.y - 5f, _anchor.z), 
                        new Vector3(stageRight, _anchor.y + 5f, _anchor.z));

        // Visualize the camera's clamp range
        if (_cam != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(new Vector3(stageLeft + _halfWidth, _anchor.y, _anchor.z), 
                            new Vector3(stageRight - _halfWidth, _anchor.y, _anchor.z));
        }
    }
#endif
}
