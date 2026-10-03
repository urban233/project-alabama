using System.Collections;
using Alabama.Driving;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Alabama.Tests
{
    public sealed class DistrictLoopTests
    {
        [UnityTest]
        public IEnumerator RoadColliderSupportsCentreAndBothDrivingLanesAroundLoop()
        {
            yield return ReviewSceneLoader.Load("DistrictLoop");
            var route = Object.FindFirstObjectByType<RouteProgress>();
            var road = GameObject.Find("Continuous 917 m road").GetComponent<MeshCollider>();
            Assert.That(route.LengthMetres, Is.InRange(900, 940));
            for (float distance = 0; distance < route.LengthMetres; distance += 15)
            {
                var centre = route.PositionAt(distance);
                var right = Vector3.Cross(Vector3.up, route.DirectionAt(distance)).normalized;
                foreach (float offset in new[] { -7f, 0f, 7f })
                {
                    var ray = new Ray(centre + right * offset + Vector3.up * 8, Vector3.down);
                    Assert.That(road.Raycast(ray, out _, 20), Is.True,
                        $"No road contact at {distance:0} m, lateral offset {offset:0} m.");
                }
            }
        }

        [UnityTest]
        public IEnumerator RepeatableSteeringCanCompleteOneClosedLap()
        {
            yield return ReviewSceneLoader.Load("DistrictLoop");
            var car = Object.FindFirstObjectByType<ArcadeCarController>();
            var route = Object.FindFirstObjectByType<RouteProgress>();
            car.GetComponent<VehicleInput>().enabled = false;
            float previousScale = Time.timeScale;
            Time.timeScale = 4;
            float beginning = Time.time;
            float furthest = 0;
            try
            {
                while (Time.time - beginning < 100 && route.CompletedLaps == 0)
                {
                    car.SetCommand(RouteFollower.Command(car, route));
                    furthest = Mathf.Max(furthest, route.DistanceMetres);
                    Assert.That(route.DistanceFromRoadMetres, Is.LessThan(14), "The car left the drivable corridor.");
                    Assert.That(car.Body.position.y, Is.InRange(-.3f, 1.8f), "The car lost road contact.");
                    yield return null;
                }
                Assert.That(route.CompletedLaps, Is.EqualTo(1),
                    $"The guided run stopped at {route.DistanceMetres:0} m after reaching {furthest:0} m.");
                Debug.Log($"District lap check: {route.LengthMetres:0.0} m completed in {Time.time - beginning:0.0} simulated seconds.");
                car.ResetToSpawn();
                route.ResetProgress();
                Assert.That(route.CompletedLaps, Is.Zero, "A car reset should restart lap tracking.");
                Assert.That(route.Fraction, Is.LessThan(.03f));
            }
            finally
            {
                car.SetCommand(default);
                Time.timeScale = previousScale;
            }
        }
    }
}
