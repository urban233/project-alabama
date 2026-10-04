using UnityEngine;

namespace Alabama.MicroSlice
{
    // Width and bank are attached to editable spline knots. Rebuild through the menu.
    public sealed class SplineRoadProfile : MonoBehaviour
    {
        public float[] knotWidths;
        public float[] bankDegrees;
        public bool sidewalks = true;
        public Vector3[] curbOpenings;
    }
}
