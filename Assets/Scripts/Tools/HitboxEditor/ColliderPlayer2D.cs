using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class ColliderPlayer2D : MonoBehaviour
{
    public ColliderClip2D data;
    public Animator animator;

    BoxCollider2D _coll;
    int _hashTime = Shader.PropertyToID("HitboxTime");

    void Awake()
    {
        _coll = GetComponent<BoxCollider2D>();
        if (!animator) animator = GetComponentInParent<Animator>();
    }

    void Update()
    {
        if (!data || !animator) return;

        // assume the runtime state uses the SAME clip as the data asset ---------
        var info = animator.GetCurrentAnimatorClipInfo(0);
        if (info.Length == 0) return;
        AnimationClip playing = info[0].clip;
        float             t   = animator.GetCurrentAnimatorStateInfo(0).normalizedTime * playing.length;

        var frame = data.GetFrame(t);
        if (frame == null || frame.boxes.Count == 0) { _coll.enabled = false; return; }
        var box = frame.boxes[0];

        _coll.enabled = true;
        _coll.size   = box.size;
        _coll.offset = box.offset;
    }
}
