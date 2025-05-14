using UnityEngine;

[DisallowMultipleComponent]
public class UMK3CameraController : MonoBehaviour
{
    [Header("Fighters")]
    [Tooltip("Your two character Transforms.")]
    public Transform fighterA;
    public Transform fighterB;

    [Header("Vertical Follow")]
    [Tooltip("Extra world-units above the highest fighter.")]
    public float verticalPadding = 2.0f;
    [Range(0f,1f), Tooltip("0 = no vertical follow; 1 = full follow.")]
    public float verticalFollowFactor = 1.0f;
    [Tooltip("Speed at which camera rises.")]
    public float riseSpeed = 8.0f;
    [Tooltip("Speed at which camera falls back.")]
    public float fallSpeed = 5.0f;

    [Header("Shake (press Z)")]
    [Tooltip("Shake magnitude when you press Z.")]
    public float testShakeMagnitude = 0.35f;
    [Tooltip("Shake duration when you press Z.")]
    public float testShakeDuration = 0.18f;

    // ─── Internal state ────────────────────
    private Vector3 _anchor;        // the camera’s start position (we keep its Y/Z)
    private Vector3 _shakeOffset;
    private float   _shakeTimer;

    private void Start()
    {
        if (fighterA == null || fighterB == null)
        {
            Debug.LogError("UMK3CameraController: assign fighterA and fighterB in the Inspector.");
            enabled = false;
            return;
        }

        // Freeze in place the camera’s starting Y and Z:
        _anchor = transform.position;
    }

    private void Update()
    {
        // Trigger shake on Z
        if (Input.GetKeyDown(KeyCode.Z))
            _shakeTimer = testShakeDuration;

        // Update shake offset
        if (_shakeTimer > 0f)
        {
            _shakeTimer   -= Time.deltaTime;
            _shakeOffset  = (Vector3)Random.insideUnitCircle * testShakeMagnitude;
        }
        else
        {
            _shakeOffset = Vector3.zero;
        }
    }

    private void LateUpdate()
    {
        // 1) Horizontal: centre X between fighters
        float midX = (fighterA.position.x + fighterB.position.x) * 0.5f;

        // 2) Compute raw desired Y = highest fighter + padding
        float highestY   = Mathf.Max(fighterA.position.y, fighterB.position.y);
        float rawTargetY = highestY + verticalPadding;

        // 3) Only move up if rawTargetY is above your start‐Y (_anchor.y)
        float finalY = _anchor.y;
        if (rawTargetY > _anchor.y)
        {
            // blend between start‐Y and rawTargetY
            float blended = _anchor.y + (rawTargetY - _anchor.y) * verticalFollowFactor;

            // smooth move toward blended
            float speed = (blended > transform.position.y) ? riseSpeed : fallSpeed;
            finalY     = Mathf.MoveTowards(transform.position.y, blended, speed * Time.deltaTime);
        }

        // 4) Build final position: X, Y, original Z + shake
        transform.position = new Vector3(midX, finalY, _anchor.z)
                           + _shakeOffset;
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
}
