using UnityEngine;

public class FighterAnimBridge : MonoBehaviour
{
    Animator anim;

    void Awake() => anim = GetComponent<Animator>();

    public void PlayTrigger(string trigger)
    {
        anim.SetTrigger(trigger);
    }
}
