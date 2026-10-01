using UnityEngine;

namespace Alabama.Driving
{
    /// <summary>Small review overlay; replaces external debugging while tuning handling.</summary>
    public sealed class DriveTelemetry : MonoBehaviour
    {
        [SerializeField] private ArcadeCarController target;
        [SerializeField] private RouteProgress route;
        private GUIStyle readout;
        private GUIStyle help;

        private void OnGUI()
        {
            if (target == null) return;
            readout ??= new GUIStyle(GUI.skin.label) { fontSize = 27, fontStyle = FontStyle.Bold };
            help ??= new GUIStyle(GUI.skin.label) { fontSize = 15 };
            readout.normal.textColor = Color.white;
            help.normal.textColor = Color.white;
            GUI.Box(new Rect(18, 18, 370, route == null ? 101 : 129), route == null ? "DRIVE TEST" : "DISTRICT LOOP");
            GUI.Label(new Rect(32, 42, 300, 40), $"{target.SpeedMetresPerSecond * 3.6f:0} km/h   {(target.IsReversing ? "R" : "D")}", readout);
            GUI.Label(new Rect(32, 83, 290, 24), $"Steer {target.SteerAngle:+0;-0;0}°", help);
            if (route != null)
                GUI.Label(new Rect(32, 108, 345, 26),
                    $"Lap {route.CompletedLaps + 1}  ·  {route.Fraction * 100:0}%  ·  {(route.DistanceFromRoadMetres > 12 ? "OFF ROAD" : $"{route.LengthMetres:0} m route")}", help);
            GUI.Box(new Rect(18, Screen.height - 70, 650, 52), "");
            GUI.Label(new Rect(30, Screen.height - 62, 620, 30),
                "WASD / arrows drive   ·   Space handbrake   ·   R reset   ·   Esc pause", help);
            if (Time.timeScale == 0) GUI.Label(new Rect(Screen.width / 2 - 70, 40, 200, 40), "PAUSED", readout);
        }
    }
}
