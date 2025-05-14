using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Demonstrates run-meter logic with a single "Run" key (M) and displays
/// the run meter and lockout timer in a UI Text or OnGUI fallback.
/// </summary>
public class RunMeterController : MonoBehaviour
{
    [Header("Run Meter Settings")]
    [Tooltip("Maximum stamina units")]                public float runMeterMax = 128f;
    [Tooltip("Units drained per second when running")] public float drainRatePerSecond = 60f;
    [Tooltip("Units restored per second when not running")] public float refillRatePerSecond = 60f;
    [Tooltip("Lockout duration (seconds) after meter empties while running")] public float crushLockoutDuration = 15f / 60f; // 15 frames @60Hz

    [Header("Input Key")]
    [Tooltip("Key to simulate run, default = M")]      public KeyCode runKey = KeyCode.M;

    [Header("UI Display (Optional)")]
    [Tooltip("Text component to display run meter and lockout")] public Text runMeterText;

    [Header("Runtime State (Read-Only)")]
    [SerializeField] private float runMeter;
    [SerializeField] private float crushTimer;

    void Awake()
    {
        // Start with full meter
        runMeter = runMeterMax;
    }

    void Update()
    {
        bool running = Input.GetKey(runKey);

        // Countdown lockout timer
        if (crushTimer > 0f)
        {
            crushTimer -= Time.deltaTime;
            if (crushTimer < 0f) crushTimer = 0f;
        }

        // Drain or refill the meter
        if (running && crushTimer <= 0f)
        {
            runMeter -= drainRatePerSecond * Time.deltaTime;
            if (runMeter <= 0f)
            {
                runMeter = 0f;
                crushTimer = crushLockoutDuration;
            }
        }
        else if (!running && crushTimer <= 0f)
        {
            runMeter += refillRatePerSecond * Time.deltaTime;
            if (runMeter > runMeterMax)
                runMeter = runMeterMax;
        }

        // Update UI Text if assigned
        if (runMeterText != null)
            UpdateUIText();
    }

    void OnGUI()
    {
        // Fallback display when no UI Text is assigned
        string display = string.Format(
            "Run Meter: {0:F1}/{1} ({2:F0}%)\nLockout: {3:F2}s\nPress '{4}' to run", 
            runMeter, runMeterMax,
            runMeterMax > 0f ? runMeter / runMeterMax * 100f : 0f,
            crushTimer, runKey);
        GUI.Label(new Rect(10, 10, 320, 60), display);
    }

    void UpdateUIText()
    {
        float percent = runMeterMax > 0f ? runMeter / runMeterMax * 100f : 0f;
        runMeterText.text = string.Format(
            "Run Meter: {0:F1}/{1} ({2:F0}%)\nLockout: {3:F2}s", 
            runMeter, runMeterMax, percent, crushTimer);
    }

    /// <summary>Time to fully empty the meter in seconds.</summary>
    public float TimeToEmpty => runMeterMax / drainRatePerSecond;
    /// <summary>Time to fully refill the meter in seconds.</summary>
    public float TimeToFull  => runMeterMax / refillRatePerSecond;
    /// <summary>Configured lockout duration in seconds.</summary>
    public float LockoutTime => crushLockoutDuration;
}
