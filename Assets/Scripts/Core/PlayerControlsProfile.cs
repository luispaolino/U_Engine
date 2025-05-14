using UnityEngine;

[CreateAssetMenu(menuName = "UMK3/PlayerControlsProfile")]
public class PlayerControlsProfile : ScriptableObject
{
    [Header("Axes (for joystick/D-pad)")]
    public string horizontalAxis = "Horizontal";
    public string verticalAxis = "Vertical";

     [Header("Optional dedicated Up key (leave None to rely purely on axis)")]
    public KeyCode leftKey   = KeyCode.None;
    public KeyCode rightKey  = KeyCode.None;
    public KeyCode upKey     = KeyCode.None;
    public KeyCode downKey   = KeyCode.None;

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
