using UnityEngine;
using UnityEngine.Rendering;

//[ExecuteAlways]
public class StageBounds : MonoBehaviour
{
    //[Header("Limits")]
    //[Tooltip("Drag your left and right stage limit markers here")]
    public Transform leftWall;   // empty at left edge
    public Transform rightWall;  // empty at right edge

    public float LeftX  => leftWall  ? leftWall.position.x  : -Mathf.Infinity;
    public float RightX => rightWall ? rightWall.position.x :  Mathf.Infinity;

    /*

    [Tooltip("Stage limit distance; leftWall moves +stageLimit, rightWall moves –stageLimit (clamped ≥ 0)")]
    public float stageLimit = 0.5f;

    [Tooltip("Height of the limit bound (in world units)")]
    public float boundHeight = 2f;

    [Tooltip("Width (and depth) of the limit bound")]
    public float boundWidth = 0.05f;

    // Semi-transparent green
    private static readonly Color FillColor = new Color(0f, 1f, 0f, 0.5f);

    // Overlay material and cube mesh for both Scene & Game view
    private static Material overlayMat;
    private static Mesh     cubeMesh;

    void OnEnable()
    {
        EnsureResources();
        ApplyStageLimit();
    }

    void OnValidate()
    {
        // Clamp stageLimit to non-negative
        stageLimit = Mathf.Max(0f, stageLimit);
        ApplyStageLimit();
    }

    void Update()
    {
        // In case stageLimit changed at runtime
        stageLimit = Mathf.Max(0f, stageLimit);
        ApplyStageLimit();
    }

    private void EnsureResources()
    {
        // Create overlay material once
        if (overlayMat == null)
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            overlayMat = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            overlayMat.SetInt("_ZTest",  (int)CompareFunction.Always);
            overlayMat.SetInt("_ZWrite", 0);
            overlayMat.SetInt("_Cull",   (int)CullMode.Off);
            overlayMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            overlayMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            overlayMat.SetColor("_Color", FillColor);
        }

        // Grab the built-in cube mesh once
        if (cubeMesh == null)
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(temp);
        }
    }

    private void ApplyStageLimit()
    {
        if (leftWall != null)
        {
            Vector3 pos = leftWall.position;
            leftWall.position = new Vector3(
                stageLimit,
                pos.y,
                pos.z
            );
        }
        if (rightWall != null)
        {
            Vector3 pos = rightWall.position;
            rightWall.position = new Vector3(
                -stageLimit,
                pos.y,
                pos.z
            );
        }
    }

    void OnRenderObject()
    {
        EnsureResources();

        if (overlayMat == null || cubeMesh == null) return;
        overlayMat.SetPass(0);

        if (leftWall != null)
            DrawBound(leftWall.position);
        if (rightWall != null)
            DrawBound(rightWall.position);
    }

    private void DrawBound(Vector3 basePos)
    {
        Vector3 center = new Vector3(
            basePos.x,
            basePos.y + boundHeight * 0.5f,
            basePos.z
        );
        Vector3 scale = new Vector3(boundWidth, boundHeight, boundWidth);
        Matrix4x4 matrix = Matrix4x4.TRS(center, Quaternion.identity, scale);
        Graphics.DrawMeshNow(cubeMesh, matrix);
    }

    void OnDrawGizmos()
    {
#if UNITY_EDITOR
        var prev = UnityEditor.Handles.zTest;
        UnityEditor.Handles.zTest = CompareFunction.Always;
#endif
        Gizmos.color = FillColor;

        if (leftWall != null)
        {
            Vector3 center = leftWall.position + Vector3.up * (boundHeight * 0.5f);
            Gizmos.DrawCube(center, new Vector3(boundWidth, boundHeight, boundWidth));
        }
        if (rightWall != null)
        {
            Vector3 center = rightWall.position + Vector3.up * (boundHeight * 0.5f);
            Gizmos.DrawCube(center, new Vector3(boundWidth, boundHeight, boundWidth));
        }

#if UNITY_EDITOR
        UnityEditor.Handles.zTest = prev;
#endif
    }
    */
}
