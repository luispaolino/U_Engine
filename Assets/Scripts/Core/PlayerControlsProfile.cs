using UnityEngine;

[CreateAssetMenu(menuName = "UMK3/PlayerControlsProfile")]
public class PlayerControlsProfile : ScriptableObject
{
    [Header("Axes (for joystick/D-pad)")]
    public string HorizontalAxis;
    public string VerticalAxis;

    [Header("Buttons")]
    public KeyCode runKey;
    public KeyCode blockKey;
    public KeyCode highPunchKey;
    public KeyCode highKickKey;
    public KeyCode lowPunchKey;
    public KeyCode lowKickKey;
    public KeyCode startKey;
    public KeyCode selectKey;
}
