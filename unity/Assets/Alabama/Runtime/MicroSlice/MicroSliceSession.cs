using System;
using Alabama.Driving;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Alabama.MicroSlice
{
    // Both return gates have index 3. The shortcut still requires the hill and arterial.
    public sealed class MicroSliceLapState
    {
        public int NextCheckpoint { get; private set; }
        public int CompletedLaps { get; private set; }
        public float LastLapSeconds { get; private set; }
        public bool Running { get; private set; }
        private float startedAt;
        public float Elapsed(float now) => Running ? Mathf.Max(0, now - startedAt) : 0;

        public bool Pass(int index, bool forward, float now)
        {
            if (!forward || index != NextCheckpoint) return false;
            if (index == 0)
            {
                if (Running) { CompletedLaps++; LastLapSeconds = now - startedAt; }
                Running = true;
                startedAt = now;
            }
            NextCheckpoint = (index + 1) % 4;
            return true;
        }
        public void Reset()
        {
            NextCheckpoint = 0; CompletedLaps = 0; LastLapSeconds = 0; Running = false;
        }
    }

    public sealed class MicroSliceSession : MonoBehaviour
    {
        [SerializeField] private ArcadeCarController vehicle;
        [SerializeField] private CooldownSanctuary sanctuary;
        [SerializeField] private BreakableStructure[] breakables;
        public MicroSliceLapState Laps { get; } = new MicroSliceLapState();
        public ArcadeCarController Vehicle => vehicle;
        private Vector3 previousPosition;
        private bool sampled;
        private GUIStyle textStyle;

        public void Configure(ArcadeCarController car, CooldownSanctuary zone, BreakableStructure[] props)
        { vehicle = car; sanctuary = zone; breakables = props; }

        public void RegisterGate(int index, Vector3 forward, Rigidbody body)
        {
            if (vehicle == null || body != vehicle.Body) return;
            Laps.Pass(index, Vector3.Dot(body.linearVelocity, forward) > .3f, Time.time);
        }

        public void ResetFixtures()
        {
            Laps.Reset();
            sanctuary?.ResetCooldown();
            if (breakables != null) foreach (var prop in breakables) if (prop != null) prop.Rearm();
        }

        private void Update()
        {
            if ((Keyboard.current?.rKey.wasPressedThisFrame ?? false) ||
                (Gamepad.current?.buttonNorth.wasPressedThisFrame ?? false)) ResetFixtures();
            if (vehicle == null) return;
            var position = vehicle.transform.position;
            if (sampled && Vector3.Distance(previousPosition, position) > 35) ResetFixtures();
            previousPosition = position; sampled = true;
            if (position.y < -10)
            {
                vehicle.ResetToSpawn();
                ResetFixtures();
            }
        }

        private void OnGUI()
        {
            textStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 17 };
            textStyle.normal.textColor = new Color(1f, .89f, .69f);
            GUILayout.BeginArea(new Rect(18, 140, 390, 142), GUI.skin.box);
            GUILayout.Label("SAFEHOUSE MICRO-SLICE", textStyle);
            GUILayout.Label($"Lap {Laps.CompletedLaps + 1}  •  {Laps.Elapsed(Time.time):0.0}s  •  target ~90s", textStyle);
            GUILayout.Label(Laps.LastLapSeconds > 0 ? $"Last lap {Laps.LastLapSeconds:0.0}s" : $"Next checkpoint {Laps.NextCheckpoint}", textStyle);
            GUILayout.Label(sanctuary != null && sanctuary.CooledDown ? "COURTYARD: COOLED DOWN" : "Courtyard: stop out of sight for 3 seconds", textStyle);
            GUILayout.EndArea();
        }
    }
}
