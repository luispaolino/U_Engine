using UnityEngine;
using System.Collections;

public class CameraFollow2D : MonoBehaviour
{
    public Transform fighterA;
    public Transform fighterB;
    public Vector2   stageBoundsX = new Vector2(-13f, 13f);
    public float     groundY      = 0f;
    [Range(0,1)] public float lerp = 0.15f;

    Vector3 _target;

    void LateUpdate()
    {
        if (!fighterA || !fighterB) return;

        float mid = (fighterA.position.x + fighterB.position.x) * 0.5f;
        mid = Mathf.Clamp(mid, stageBoundsX.x, stageBoundsX.y);

        _target = new Vector3(mid, groundY, transform.position.z);
        transform.position = Vector3.Lerp(transform.position, _target, lerp);
    }

    public void PanVertical(float yDest, float time)
        => StartCoroutine(PanRoutine(yDest, time));

    IEnumerator PanRoutine(float yDest, float t)
    {
        var start = _target;
        var end   = new Vector3(_target.x, yDest, _target.z);
        float elapsed = 0;
        while (elapsed < t)
        {
            elapsed += Time.deltaTime;
            _target  = Vector3.Lerp(start, end, elapsed / t);
            yield return null;
        }
        _target = end;
    }
}
