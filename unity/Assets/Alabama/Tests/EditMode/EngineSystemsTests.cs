using Alabama.Districts;
using NUnit.Framework;
using UnityEngine;

namespace Alabama.Tests
{
    public sealed class EngineSystemsTests
    {
        [Test]
        public void SceneryDemandIncludesFutureTravelAndKeepsAnUnloadMargin()
        {
            var cell = new DistrictVisualStreamer.Cell { bounds = new Bounds(Vector3.zero,Vector3.one*10),visibleDistance = 200 };
            Assert.That(DistrictVisualStreamer.Required(cell,Vector3.right*500,Vector3.right*220,100),Is.True);
            Assert.That(DistrictVisualStreamer.Required(cell,Vector3.right*500,Vector3.right*500,100),Is.False);
            Assert.That(DistrictVisualStreamer.Required(cell,Vector3.right*420,Vector3.right*420,250),Is.True);
        }
        [Test]
        public void RoadHeadingUsesElevationAndPreservesTheClosestDrivingDirection()
        {
            var owner = new GameObject("Designated road direction test");
            try
            {
                var roads = owner.AddComponent<DrivableRoadMap>();
                roads.Configure(null,3,new[] {
                    new DrivableRoadMap.Direction { start = new Vector3(-50,0,0),end = new Vector3(50,0,0) },
                    new DrivableRoadMap.Direction { start = new Vector3(0,10,-50),end = new Vector3(0,10,50) } });
                Assert.That(Vector3.Dot(roads.Heading(new Vector3(0,10,2),Vector3.back),Vector3.back),Is.GreaterThan(.99f));
                Assert.That(Vector3.Dot(roads.Heading(new Vector3(2,0,0),Vector3.left),Vector3.left),Is.GreaterThan(.99f));
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
