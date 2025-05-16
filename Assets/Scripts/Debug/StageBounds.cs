using UnityEngine;
using UnityEngine.Rendering;

public class StageBounds : MonoBehaviour
{
    public Transform leftWall;
    public Transform rightWall;

    public float LeftX  => leftWall  ? leftWall.position.x  : -Mathf.Infinity;
    public float RightX => rightWall ? rightWall.position.x :  Mathf.Infinity;

}
