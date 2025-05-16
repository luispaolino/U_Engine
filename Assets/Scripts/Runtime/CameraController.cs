using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class CameraController : MonoBehaviour
{
    [Header("Player Targets")]

    public Transform player1;
    public Transform player2;

    public Transform leftWall;
    public Transform rightWall;

    //public float LeftX  => leftWall  ? leftWall.position.x  : -Mathf.Infinity;
    //public float RightX => rightWall ? rightWall.position.x :  Mathf.Infinity;

    [Header("Stage Limits")]
    public float stageLeft = -10f;
    public float stageRight = 10f;

    [Header("Vertical Follow")]
    public float verticalPadding = 2.0f;
    public float riseSpeed = 8.0f;
    public float fallSpeed = 5.0f;
    [Range(0f, 1f)] public float verticalFollowFactor = 1.0f;

    [Header("Shake (press Z - for testing)")]
    public float testShakeMagnitude = 0.35f;
    public float testShakeDuration = 0.18f;

    private Vector3 _anchor;
    private Vector3 _shakeOffset;
    private float   _shakeTimer;
    private Camera  _cam;
    private float   _halfWidth;
    private bool    _isInitialized = false;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null)
        {
            Debug.LogError("CameraController: Must be attached to a Camera object. Disabling.", this);
            enabled = false;
            return;
        }
        _anchor = transform.position;

        leftWall.position = new Vector3(stageLeft, leftWall.position.y, leftWall.position.z);
        rightWall.position = new Vector3(stageRight, rightWall.position.y, rightWall.position.z);
    }

    public void InitializePlayerTargets(Transform p1Transform, Transform p2Transform)
    {
        if (_isInitialized && player1 == p1Transform && player2 == p2Transform)
        {
            // Already initialized with the same targets, no need to re-do.
            // This can happen if RoundSystem calls it multiple times (e.g. after round resets without changing player instances)
            return;
        }

        player1 = p1Transform; // Use new field name
        player2 = p2Transform; // Use new field name

        if (player1 == null || player2 == null)
        {
            Debug.LogError("CameraController: InitializePlayerTargets called with null player transform(s). Camera may not function correctly.", this);
            _isInitialized = false; // Ensure it's not marked as initialized
            enabled = false; // Or just don't set _isInitialized = true;
            return;
        }
        
        if (_cam == null) // Should have been set in Awake, but double check
        {
             _cam = GetComponent<Camera>();
             if (_cam == null) { Debug.LogError("CameraController: Camera component missing!", this); enabled = false; return;}
             _anchor = transform.position;
        }

        UpdateCameraHalfWidth();
        _isInitialized = true;
        Debug.Log("CameraController: Player targets initialized.");
    }

    private void Update()
    {
        if (!_isInitialized) return;

        // Test Shake Input
        if (Input.GetKeyDown(KeyCode.Z))
        {
            // If already shaking, this will restart it with test values
            StartShake(testShakeMagnitude, testShakeDuration);
        }

        if (_shakeTimer > 0f)
        {
            _shakeTimer -= Time.deltaTime;
            _shakeOffset = (Vector3)Random.insideUnitCircle * testShakeMagnitude; // Use the current magnitude for the shake
            if (_shakeTimer <= 0f)
            {
                _shakeOffset = Vector3.zero; // Ensure shake stops precisely
            }
        }
        // No else needed here for _shakeOffset = Vector3.zero, as it's handled when timer expires.
    }

    private void LateUpdate()
    {
        if (!_isInitialized || player1 == null || player2 == null) // Check new field names
        {
            return;
        }

        // 1) Horizontal: Center X between players
        float midX = (player1.position.x + player2.position.x) * 0.5f; // Use new field names

        if (_cam != null && Mathf.Abs(_halfWidth) < 0.001f) // Recalculate if seems zero or camera changed
        {
            UpdateCameraHalfWidth();
        }
        midX = Mathf.Clamp(midX, stageLeft + _halfWidth, stageRight - _halfWidth);

        // 2) Vertical: Track the higher player
        float desiredCamY;
        float highestFighterY = Mathf.Max(player1.position.y, player2.position.y); // Use new field names

        if (highestFighterY + verticalPadding > _anchor.y && verticalFollowFactor > 0)
        {
            float rawFollowY = highestFighterY + verticalPadding;
            desiredCamY = _anchor.y + (rawFollowY - _anchor.y) * verticalFollowFactor;
        }
        else
        {
            desiredCamY = _anchor.y;
        }

        float currentCamY = transform.position.y;
        float newCamY;

        if (Mathf.Approximately(currentCamY, desiredCamY))
        {
            newCamY = desiredCamY;
        }
        else
        {
            float speed = desiredCamY > currentCamY ? riseSpeed : fallSpeed;
            newCamY = Mathf.MoveTowards(currentCamY, desiredCamY, speed * Time.deltaTime);
        }

        // 3) Apply final position
        transform.position = new Vector3(midX, newCamY, _anchor.z) + _shakeOffset;
    }

    private void UpdateCameraHalfWidth()
    {
        if (_cam == null) return;

        if (_cam.orthographic)
        {
            _halfWidth = _cam.orthographicSize * _cam.aspect;
        }
        else // Perspective camera
        {
            // Use player1 or player2 if available for a more accurate distance to projection plane,
            // otherwise default to the camera's own z distance from world origin (if players are near z=0).
            float distanceToProjectionPlane = Mathf.Abs(transform.position.z); // Default distance
            if (player1 != null) // Use new field name
            {
                 distanceToProjectionPlane = Mathf.Abs(transform.position.z - player1.position.z);
            }
            else if (player2 != null) // Use new field name
            {
                 distanceToProjectionPlane = Mathf.Abs(transform.position.z - player2.position.z);
            }
            // Ensure distance is positive
            if (distanceToProjectionPlane < 0.01f) distanceToProjectionPlane = 0.01f;


            float halfHeightAtDistance = Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * distanceToProjectionPlane;
            _halfWidth = halfHeightAtDistance * _cam.aspect;
        }
        
        if (Mathf.Abs(_halfWidth) < 0.01f && Application.isPlaying && _isInitialized) { // Only warn if initialized and width is bad
            Debug.LogWarning($"CameraController: Calculated _halfWidth is very small ({_halfWidth}). Check camera settings, aspect ratio, or Z distance to players.", this);
        }
    }

    public void StartShake(float magnitude, float duration)
    {
        this.testShakeMagnitude = magnitude; // Update the magnitude used by the ongoing shake
        this.testShakeDuration = duration;   // Store duration if needed, though not directly used by countdown
        _shakeTimer = duration;
        if (duration <= 0) _shakeOffset = Vector3.zero; // Stop shake immediately if duration is zero/negative
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected() 
    { 
        // Use _anchor if available, otherwise current position for Y/Z in editor
        float gizmoAnchorY = Application.isPlaying ? _anchor.y : transform.position.y;
        float gizmoAnchorZ = Application.isPlaying ? _anchor.z : transform.position.z;

        Gizmos.color = Color.yellow; 
        Gizmos.DrawLine(new Vector3(stageLeft, gizmoAnchorY - 5f, gizmoAnchorZ), new Vector3(stageLeft, gizmoAnchorY + 5f, gizmoAnchorZ)); 
        Gizmos.DrawLine(new Vector3(stageRight, gizmoAnchorY - 5f, gizmoAnchorZ), new Vector3(stageRight, gizmoAnchorY + 5f, gizmoAnchorZ)); 

        leftWall.position = new Vector3(stageLeft, leftWall.position.y, leftWall.position.z);
        rightWall.position = new Vector3(stageRight, rightWall.position.y, rightWall.position.z);
        
        if (_cam != null) 
        { 
            // Recalculate halfwidth for Gizmos if not playing or if it's zero, using current player refs if available
            if (!Application.isPlaying || Mathf.Abs(_halfWidth) < 0.001f ) UpdateCameraHalfWidth();
            Gizmos.color = Color.cyan; 
            Gizmos.DrawLine(new Vector3(stageLeft + _halfWidth, gizmoAnchorY, gizmoAnchorZ), new Vector3(stageRight - _halfWidth, gizmoAnchorY, gizmoAnchorZ)); 
        } 
    }
#endif
}