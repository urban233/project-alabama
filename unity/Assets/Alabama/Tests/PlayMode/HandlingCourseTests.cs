using System.Collections;
using Alabama.Driving;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Alabama.Tests
{
    public sealed class HandlingCourseTests
    {
        [UnityTest]
        public IEnumerator CarAcceleratesBrakesReversesAndResetsOnTheSavedStreet()
        {
            yield return SceneManager.LoadSceneAsync("HandlingCourse", LoadSceneMode.Single);
            var car = Object.FindFirstObjectByType<ArcadeCarController>();
            Assert.That(car, Is.Not.Null);
            car.GetComponent<VehicleInput>().enabled = false;
            var body = car.Body;
            Assert.That(body, Is.Not.Null);
            for (int i = 0; i < 120; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.position.y, Is.InRange(-.1f, .7f), "Car should settle on its four tyres.");
            // An FBX can look correct in edit mode yet turn its wheel meshes flat
            // when the controller applies WheelCollider poses in play mode.
            foreach (var wheel in car.GetComponentsInChildren<Transform>())
            {
                if (!wheel.name.StartsWith("Wheel_")) continue;
                var renderer = wheel.GetComponentInChildren<MeshRenderer>();
                if (renderer == null) continue;
                Assert.That(renderer.bounds.size.y, Is.GreaterThan(car.Tuning.WheelRadius * 1.5f),
                    "The wheel visual must remain upright after applying the physics pose: " + wheel.name);
                Assert.That(Vector3.Distance(renderer.bounds.center, wheel.position), Is.LessThan(.06f),
                    "The wheel mesh must rotate around its measured axle: " + wheel.name);
            }
            var start = body.position;
            car.SetCommand(new VehicleCommand(1, 0, 0, false));
            for (int i = 0; i < 360; i++) yield return new WaitForFixedUpdate();
            float movingSpeed = car.SignedForwardSpeed;
            Debug.Log($"Handling acceleration check: {movingSpeed * 3.6f:0.0} km/h after 3 seconds.");
            Assert.That(movingSpeed, Is.GreaterThan(7), "The rear axle should accelerate the car on asphalt.");
            Assert.That(body.position.z, Is.GreaterThan(start.z + 8), "The car should travel down the test street.");
            car.SetCommand(new VehicleCommand(0, 1, 0, false));
            for (int i = 0; i < 240; i++) yield return new WaitForFixedUpdate();
            Assert.That(car.SignedForwardSpeed, Is.LessThan(movingSpeed - 5), "Service brakes should reduce forward speed.");
            for (int i = 0; i < 150; i++) yield return new WaitForFixedUpdate();
            Assert.That(car.SignedForwardSpeed, Is.LessThan(-.4f), "Holding brake at rest should engage reverse.");
            car.SetCommand(new VehicleCommand(0, 0, 1, false));
            yield return new WaitForFixedUpdate();
            Assert.That(car.SteerAngle, Is.GreaterThan(8), "Low-speed steering should turn the front axle.");
            car.ResetToSpawn();
            Assert.That(body.position.z, Is.EqualTo(2).Within(.05f));
            Assert.That(body.linearVelocity.magnitude, Is.LessThan(.05f));
            Assert.That(float.IsFinite(body.position.x) && float.IsFinite(body.position.y) && float.IsFinite(body.position.z), Is.True);
        }

        [UnityTest]
        public IEnumerator StraightAtHighSpeedKeepsContactAndDoesNotLeaveTheRoad()
        {
            yield return SceneManager.LoadSceneAsync("HandlingCourse", LoadSceneMode.Single);
            var car = Object.FindFirstObjectByType<ArcadeCarController>();
            car.GetComponent<VehicleInput>().enabled = false;
            for (int i = 0; i < 100; i++) yield return new WaitForFixedUpdate();
            var body = car.Body;
            // Launch at 200.2 km/h so every physics step exercises high-speed road contact.
            body.linearVelocity = car.transform.forward * 55.6f;
            car.SetCommand(new VehicleCommand(.5f, 0, 0, false));
            for (int i = 0; i < 240; i++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(body.position.x, Is.InRange(-3.5f, -1.1f), "The car left its lane at high speed.");
                Assert.That(body.position.y, Is.InRange(-.2f, 1f), "The suspension lost road height.");
            }
            Assert.That(body.position.z, Is.GreaterThan(90), "The vehicle should cover the high-speed straight.");
            Assert.That(float.IsFinite(body.linearVelocity.magnitude), Is.True);
            Debug.Log($"Handling high-speed check: {body.position.z:0.0} m down street, {car.SpeedMetresPerSecond * 3.6f:0.0} km/h.");
        }

        [UnityTest]
        public IEnumerator WheelContactsCurbAndChassisIsContainedByRoadBarrier()
        {
            yield return SceneManager.LoadSceneAsync("HandlingCourse", LoadSceneMode.Single);
            var car = Object.FindFirstObjectByType<ArcadeCarController>();
            car.GetComponent<VehicleInput>().enabled = false;
            var body = car.Body;
            body.position = new Vector3(6.1f, .38f, 43);
            body.rotation = Quaternion.identity;
            body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            bool curbContact = false;
            for (int i = 0; i < 180; i++)
            {
                yield return new WaitForFixedUpdate();
                foreach (var wheel in car.GetComponentsInChildren<WheelCollider>())
                    if (wheel.GetGroundHit(out var hit) && hit.collider.name == "Suspension test curb") curbContact = true;
            }
            Assert.That(curbContact, Is.True, "At least one tyre should contact the test curb.");
            Assert.That(body.position.y, Is.InRange(0, 1.2f), "Curb contact must not destabilize the chassis.");
            body.position = new Vector3(5.8f, .22f, 67);
            body.rotation = Quaternion.identity;
            body.linearVelocity = new Vector3(8, 0, 5);
            body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
            for (int i = 0; i < 120; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.position.x, Is.LessThan(8.2f), "The roadside barrier should contain the chassis.");
            Assert.That(float.IsFinite(body.linearVelocity.magnitude), Is.True);
        }
    }
}
