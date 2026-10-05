#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using Alabama.Bootstrap;
using Alabama.Districts;
using Alabama.Driving;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Alabama.Tests
{
    public sealed class DistrictLifetimeTests
    {
        private const string Root = "Assets/Alabama/Art/Maps/NfsWorld/Runtime/";
        private const string ArtDirectionContent = "Assets/Alabama/Art/Maps/NfsWorld/ArtDirection/Runtime/DowntownContent.unity";
        private EditorBuildSettingsScene[] previousScenes;
        private DistrictRuntime runtime;
        private ArcadeCarController car;
        private ChaseCamera chase;
        private VehicleInput input;
        private UnityEngine.Rendering.RenderPipelineAsset pipeline;
        private Color fog;
        private float density;
        private int cap;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Debug.Log("District lifetime test: " + TestContext.CurrentContext.Test.Name);
            Assert.That(File.Exists(Root + "FixtureRuntime.unity"), Is.True, "Run RuntimeSetup with the pinned private assets.");
            previousScenes = EditorBuildSettings.scenes;
            EditorBuildSettings.scenes = previousScenes.Concat(new[] { "DowntownContent", "FixtureA", "FixtureB",
                "FixtureDuplicate", "FixtureUnsupported" }.Select(n => new EditorBuildSettingsScene(Root + n + ".unity", true)))
                .Concat(new[] { ArtDirectionContent }.Where(File.Exists).Select(p => new EditorBuildSettingsScene(p, true)))
                .GroupBy(s => s.path).Select(g => g.Last()).ToArray();
            yield return EditorSceneManager.LoadSceneInPlayMode(Root + "FixtureRuntime.unity", new LoadSceneParameters(LoadSceneMode.Single));
            runtime = DistrictRuntime.Instance;
            Assert.That(runtime, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 90;
            while (!runtime.Ready && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(runtime.Ready, Is.True, runtime.LastFailure);
            car = runtime.Player; chase = runtime.Chase; input = car.GetComponent<VehicleInput>();
            pipeline = QualitySettings.renderPipeline; fog = RenderSettings.fogColor;
            density = RenderSettings.fogDensity; cap = Application.targetFrameRate;
            AssertOwners();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            SceneManager.SetActiveScene(SceneManager.CreateScene("District test cleanup"));
            // Remove content before host teardown so disable hooks exercise actual lifetime.
            var loaded = runtime == null ? new DistrictContent[0] : runtime.LoadedDistricts.ToArray();
            foreach (var content in loaded)
                if (content != null) yield return SceneManager.UnloadSceneAsync(content.gameObject.scene);
            if (runtime != null) yield return SceneManager.UnloadSceneAsync(runtime.gameObject.scene);
            if (previousScenes != null) EditorBuildSettings.scenes = previousScenes;
        }

        private void AssertOwners()
        {
            Assert.That(Object.FindObjectsByType<ArcadeCarController>(FindObjectsSortMode.None), Is.EqualTo(new[] { car }));
            Assert.That(Object.FindObjectsByType<VehicleInput>(FindObjectsSortMode.None), Is.EqualTo(new[] { input }));
            Assert.That(Object.FindObjectsByType<ChaseCamera>(FindObjectsSortMode.None), Is.EqualTo(new[] { chase }));
            Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<DemoBootstrap>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<NfsWorldRenderSettings>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.type == LightType.Directional), Is.EqualTo(1));
            Assert.That(input.enabled, Is.True);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(runtime.gameObject.scene));
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(pipeline));
            Assert.That(RenderSettings.fogColor, Is.EqualTo(fog));
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(density));
            Assert.That(Application.targetFrameRate, Is.EqualTo(cap));
        }

        [UnityTest]
        public IEnumerator BoundaryStopsForwardReverseAndSidewaysEscapes()
        {
            input.enabled = false;
            var content = runtime.LoadedDistricts.Single();
            var guard = car.GetComponent<DistrictBoundaryGuard>();
            var centre = content.RecoveryPoses[0].position;
            foreach (var direction in new[] { Vector3.forward, Vector3.back, Vector3.right,
                Vector3.left, new Vector3(1, 0, 1).normalized })
            {
                float edge = 50 / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.z));
                car.Body.position = centre + direction * (edge - 4);
                car.Body.rotation = Quaternion.identity;
                car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
                car.SetCommand(default); Physics.SyncTransforms();
                for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
                int blocked = guard.BlockedMoves;
                car.Body.linearVelocity = direction * 40;
                for (int i = 0; i < 80; i++)
                {
                    yield return new WaitForFixedUpdate();
                    Assert.That(guard.ContainsFootprint(car.Body.position, car.Body.rotation,
                        scene => scene == content.gameObject.scene), Is.True, "Escaped in direction " + direction);
                }
                Assert.That(guard.BlockedMoves, Is.GreaterThan(blocked));
                // A boundary prevents escape, while safe tangential crash motion may continue.
                // The footprint assertion on every measured step is the relevant contract.
            }
            runtime.Recover(); input.enabled = true;
        }

        [UnityTest]
        public IEnumerator CarCanReverseAndTurnAfterStoppingWhereOnlyTheMarginLeavesTheRoad()
        {
            input.enabled = false;
            var centre = runtime.LoadedDistricts.Single().RecoveryPoses[0].position;
            var guard = car.GetComponent<DistrictBoundaryGuard>();
            car.Body.position = centre + Vector3.right * 47.65f;
            car.Body.rotation = Quaternion.Euler(0, 90, 0);
            car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
            guard.ResetHistory(); car.SetCommand(default); Physics.SyncTransforms();
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            var stopped = car.Body.position;
            Assert.That(stopped.x-centre.x, Is.GreaterThan(45), "Guard must not send the car to the start.");
            car.SetCommand(new VehicleCommand(0, 1, .35f, false));
            for (float elapsed = 0; elapsed < 2; elapsed += Time.fixedDeltaTime)
                yield return new WaitForFixedUpdate();
            Assert.That(stopped.x-car.Body.position.x, Is.GreaterThan(2), "Reverse must escape the edge without resetting.");
            Assert.That(Quaternion.Angle(car.Body.rotation, Quaternion.Euler(0,90,0)), Is.GreaterThan(5), "Steering must remain usable.");
            Assert.That(Vector3.Distance(car.Body.position, stopped), Is.LessThan(20), "Recovery must stay near the crash.");
            car.SetCommand(default);
        }

        [UnityTest]
        public IEnumerator RecoveryUsesRecentClearRoadRatherThanDistrictStart()
        {
            input.enabled = false;
            var centre = runtime.LoadedDistricts.Single().RecoveryPoses[0].position;
            var guard = car.GetComponent<DistrictBoundaryGuard>();
            car.Body.position = centre + new Vector3(20,0,10); car.Body.rotation = Quaternion.identity;
            car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
            guard.ResetHistory(); car.SetCommand(new VehicleCommand(0,0,0,true)); Physics.SyncTransforms();
            for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();
            var recent = car.Body.position;
            car.Body.position = recent + Vector3.up;
            car.Body.rotation = Quaternion.Euler(0,0,90); Physics.SyncTransforms();
            Assert.That(runtime.RecoverNearby(), Is.True);
            Assert.That(Vector3.Distance(car.Body.position, recent), Is.LessThan(2));
            Assert.That(Vector3.Distance(car.Body.position, centre), Is.GreaterThan(15));
            Assert.That(Vector3.Dot(car.transform.up, Vector3.up), Is.GreaterThan(.99f));
        }

        [UnityTest]
        public IEnumerator CameraSnapUsesCurrentBodyPoseAfterRepositioning()
        {
            input.enabled = false;
            car.Body.position += Vector3.right*20;
            car.Body.rotation = Quaternion.Euler(0,90,0);
            car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
            var offset = new SerializedObject(chase).FindProperty("followOffset").vector3Value;
            var expected = car.Body.position + car.Body.rotation*offset;
            chase.SnapToTarget();
            Assert.That(Vector3.Distance(chase.transform.position,expected), Is.LessThan(.05f));
            yield return null;
            Assert.That(Vector3.Distance(chase.transform.position,expected), Is.LessThan(.1f),
                "Interpolation must not pull a snapped camera back to the old district pose.");
        }

        [UnityTest]
        public IEnumerator RecoveryWithoutRecordedHistoryFindsNearbyRoadInsteadOfTheStart()
        {
            input.enabled = false;
            var centre = runtime.LoadedDistricts.Single().RecoveryPoses[0].position;
            var guard = car.GetComponent<DistrictBoundaryGuard>();
            foreach (float height in new[] { 1f,7f })
            {
                car.Body.position = centre + new Vector3(20,height,10);
                car.Body.rotation = Quaternion.Euler(0,0,90);
                car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
                guard.ResetHistory(); Physics.SyncTransforms();
                Assert.That(guard.RecoveryPoseCount, Is.Zero);
                Assert.That(runtime.RecoverNearby(), Is.True);
                Assert.That(Vector3.Distance(car.Body.position,centre+new Vector3(20,0,10)), Is.LessThan(2));
                Assert.That(Vector3.Dot(car.transform.up,Vector3.up), Is.GreaterThan(.99f));
                for (int i = 0; i < 90; i++) yield return new WaitForFixedUpdate();
                Assert.That(car.GetComponentsInChildren<WheelCollider>().Count(w=>w.GetGroundHit(out _)), Is.EqualTo(4));
            }
        }

        [UnityTest]
        public IEnumerator AirborneSupportStillRejectsEmptyAndForeignMapGround()
        {
            var content = runtime.LoadedDistricts.Single();
            var guard = car.GetComponent<DistrictBoundaryGuard>();
            var above = content.RecoveryPoses[0].position+Vector3.up*5;
            bool Owned(Scene scene) => scene == content.gameObject.scene;
            Assert.That(guard.ContainsFootprint(above,Quaternion.identity,Owned), Is.False);
            Assert.That(guard.ContainsFootprint(above,Quaternion.identity,Owned,maximumDrop:12), Is.True);
            Assert.That(guard.ContainsFootprint(above+Vector3.right*100,Quaternion.identity,Owned,maximumDrop:12), Is.False);
            Assert.That(guard.ContainsFootprint(above,Quaternion.identity,scene => !Owned(scene),maximumDrop:12), Is.False);
            Assert.That(guard.ContainsFootprint(above+Vector3.up*20,Quaternion.identity,Owned,maximumDrop:1000), Is.False);
            yield break;
        }

        [UnityTest]
        public IEnumerator AirborneRollKeepsMomentumAndUprightCameraWithoutPrematureRecovery()
        {
            input.enabled = false;
            var guard = car.GetComponent<DistrictBoundaryGuard>();
            var centre = runtime.LoadedDistricts.Single().RecoveryPoses[0].position;
            car.Body.position = centre + Vector3.right*20;
            car.Body.rotation = Quaternion.identity;
            car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
            guard.ResetHistory(); car.SetCommand(default); Physics.SyncTransforms();
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
            int recovered = guard.LocalRecoveries;
            car.Body.position += Vector3.up*5;
            car.Body.rotation = Quaternion.Euler(0,0,180);
            car.Body.linearVelocity = Vector3.forward*30;
            car.Body.angularVelocity = Vector3.forward*3;
            Physics.SyncTransforms(); chase.SnapToTarget();
            Assert.That(chase.transform.position.y, Is.GreaterThan(car.Body.position.y+1));
            float topSpeed = car.Tuning.MaximumSpeedMetresPerSecond;
            for (int i = 0; i < 45; i++) yield return new WaitForFixedUpdate();
            Assert.That(guard.LocalRecoveries, Is.EqualTo(recovered), "An ongoing roll must not be reset prematurely.");
            Assert.That(car.Body.linearVelocity.z, Is.GreaterThan(28), "Airborne momentum must not be cancelled by road protection.");
            Assert.That(car.Body.position.y, Is.LessThan(centre.y+5), "An overturned car must not receive upward downforce.");
            Assert.That(car.Tuning.MaximumSpeedMetresPerSecond, Is.EqualTo(topSpeed));
        }

        [UnityTest]
        public IEnumerator AutomaticGraphicsUsePrivatePipelineAndRestoreWithoutChangingTheCar()
        {
            var original = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)pipeline;
            float scale = original.renderScale, shadows = original.shadowDistance;
            float fixedStep = Time.fixedDeltaTime, topSpeed = car.Tuning.MaximumSpeedMetresPerSecond, torque = car.Tuning.DriveTorque;
            var host = new GameObject("Automatic graphics ownership fixture");
            var automatic = host.AddComponent<AutomaticPerformance>();
            Assert.That(automatic.Initialize(original), Is.True);
            Assert.That(QualitySettings.renderPipeline, Is.Not.SameAs(original));
            Assert.That(original.renderScale, Is.EqualTo(scale));
            Assert.That(original.shadowDistance, Is.EqualTo(shadows));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep));
            Assert.That(car.Tuning.MaximumSpeedMetresPerSecond, Is.EqualTo(topSpeed));
            Assert.That(car.Tuning.DriveTorque, Is.EqualTo(torque));
            automatic.enabled = false;
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(original));
            Assert.That(Application.targetFrameRate, Is.EqualTo(cap));
            Object.Destroy(host); yield return null;
        }

        [UnityTest]
        public IEnumerator EdgeStopsOutwardMomentumButPreservesTravelAlongTheRoad()
        {
            input.enabled = false;
            var centre = runtime.LoadedDistricts.Single().RecoveryPoses[0].position;
            var guard = car.GetComponent<DistrictBoundaryGuard>();
            car.Body.position = centre+Vector3.right*47.65f;
            car.Body.rotation = Quaternion.Euler(0,90,0);
            car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
            guard.ResetHistory(); car.SetCommand(default); Physics.SyncTransforms();
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            var start = car.Body.position;
            int blocked = guard.BlockedMoves;
            for (int i = 0; i < 90; i++)
            {
                car.Body.linearVelocity = new Vector3(8,car.Body.linearVelocity.y,10);
                yield return new WaitForFixedUpdate();
            }
            // Stop the artificial per-step outward impulses, then allow their last contact
            // correction to be handled just as an actual completed impact would be.
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(guard.BlockedMoves, Is.GreaterThan(blocked));
            Assert.That(car.Body.position.z-start.z, Is.GreaterThan(5), "Safe motion along an edge must not be zeroed.");
            Assert.That(guard.ContainsFootprint(car.Body.position,car.Body.rotation,
                scene => runtime.LoadedDistricts.Any(d => d.gameObject.scene == scene)), Is.True,
                $"Edge pose {car.Body.position}, rotation {car.Body.rotation.eulerAngles}, velocity {car.Body.linearVelocity}, blocks {guard.BlockedMoves}, recoveries {guard.LocalRecoveries}");
            Assert.That(guard.LocalRecoveries, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SidewaysAndUpsideDownCrashesRecoverNearTheirRoadPosition()
        {
            input.enabled = false;
            var centre = runtime.LoadedDistricts.Single().RecoveryPoses[0].position;
            var guard = car.GetComponent<DistrictBoundaryGuard>();
            foreach (float roll in new[] { 90f,180f })
            {
                car.Body.position = centre+new Vector3(20,0,10); car.Body.rotation = Quaternion.identity;
                car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
                guard.ResetHistory(); car.SetCommand(new VehicleCommand(0,0,0,true)); Physics.SyncTransforms();
                for (int i = 0; i < 90; i++) yield return new WaitForFixedUpdate();
                var road = car.Body.position;
                car.Body.position = road+Vector3.up; car.Body.rotation = Quaternion.Euler(0,0,roll);
                car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
                Physics.SyncTransforms(); int recovered = guard.LocalRecoveries;
                for (float elapsed = 0; elapsed < 4.5f; elapsed += Time.fixedDeltaTime)
                { car.SetCommand(new VehicleCommand(0,1,.2f,false)); yield return new WaitForFixedUpdate(); }
                Assert.That(guard.LocalRecoveries, Is.GreaterThan(recovered), $"A {roll} degree crash must not trap the car.");
                Assert.That(Vector3.Distance(car.Body.position,road), Is.LessThan(12));
                Assert.That(Vector3.Dot(car.transform.up,Vector3.up), Is.GreaterThan(.9f));
                Assert.That(guard.ContainsFootprint(car.Body.position,car.Body.rotation,
                    scene => runtime.LoadedDistricts.Any(d => d.gameObject.scene == scene)), Is.True);
                car.SetCommand(default);
            }
        }

        [UnityTest]
        public IEnumerator ContactImpulseAfterPredictionCannotPushAChassisCornerOffTheMap()
        {
            input.enabled = false;
            var centre = runtime.LoadedDistricts.Single().RecoveryPoses[0].position;
            var guard = car.GetComponent<DistrictBoundaryGuard>();
            car.Body.position = centre+Vector3.right*47.3f;
            car.Body.rotation = Quaternion.Euler(0,90,0);
            car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
            guard.ResetHistory(); car.SetCommand(default); Physics.SyncTransforms();
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            var start = car.Body.position; int blocked = guard.BlockedMoves;
            var impulse = car.gameObject.AddComponent<LateBoundaryImpulse>(); impulse.Body = car.Body;
            for (int i = 0; i < 90; i++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(guard.ContainsFootprint(car.Body.position,car.Body.rotation,
                    scene => runtime.LoadedDistricts.Any(d => d.gameObject.scene == scene)), Is.True,
                    $"A post-prediction impulse crossed the map at step {i}.");
            }
            Object.Destroy(impulse);
            Assert.That(guard.BlockedMoves, Is.GreaterThan(blocked));
            Assert.That(car.Body.position.z-start.z, Is.GreaterThan(5));
            Assert.That(guard.LocalRecoveries, Is.Zero);
        }

        [UnityTest]
        public IEnumerator HighCentredCarAutomaticallyRecoversNearTheCrashWhenTryingToDrive()
        {
            input.enabled = false;
            var content = runtime.LoadedDistricts.Single();
            var guard = car.GetComponent<DistrictBoundaryGuard>();
            var centre = content.RecoveryPoses[0].position;
            car.Body.position = centre + Vector3.right * 20;
            car.Body.rotation = Quaternion.identity;
            car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
            guard.ResetHistory(); car.SetCommand(new VehicleCommand(0,0,0,true)); Physics.SyncTransforms();
            for (int i = 0; i < 90; i++) yield return new WaitForFixedUpdate();
            var road = car.Body.position;
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(post, content.gameObject.scene);
            post.transform.position = road + new Vector3(0,.65f,3);
            post.transform.localScale = new Vector3(.5f,1,.6f);
            car.Body.position = road + new Vector3(0,1,3);
            car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
            int recovered = guard.LocalRecoveries;
            for (float elapsed = 0; elapsed < 3; elapsed += Time.fixedDeltaTime)
            {
                car.SetCommand(new VehicleCommand(0,1,0,false));
                yield return new WaitForFixedUpdate();
            }
            Assert.That(guard.LocalRecoveries, Is.GreaterThan(recovered), "Drive input must release a car with its driven wheels off the road.");
            Assert.That(Vector3.Distance(car.Body.position, road), Is.LessThan(10));
            Assert.That(Vector3.Distance(car.Body.position, centre), Is.GreaterThan(15), "Recovery must preserve the crash location.");
            Object.Destroy(post); car.SetCommand(default);
        }

        [UnityTest]
        public IEnumerator BoundaryIgnoresForeignGroundAndPermitsShortCrestJumps()
        {
            var guard = car.GetComponent<DistrictBoundaryGuard>();
            var content = runtime.LoadedDistricts.Single();
            var pose = content.RecoveryPoses[0];
            Assert.That(guard.ContainsFootprint(pose.position + Vector3.up * 2, pose.Rotation,
                scene => scene == content.gameObject.scene), Is.True);
            Assert.That(guard.ContainsFootprint(pose.position, pose.Rotation, scene => false), Is.False);
            // An overhead deck above empty space cannot count as ground beneath the car.
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(roof, content.gameObject.scene);
            var outside = pose.position + Vector3.right * 55;
            roof.transform.position = outside + Vector3.up * 2;
            roof.transform.localScale = new Vector3(8, .2f, 8); Physics.SyncTransforms();
            Assert.That(guard.ContainsFootprint(outside, pose.Rotation,
                scene => scene == content.gameObject.scene), Is.False);
            Object.Destroy(roof);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BoundaryRejectsInvisibleCollisionBeyondVisibleRoadAndDeepLowerGround()
        {
            var content = runtime.LoadedDistricts.Single();
            var guard = car.GetComponent<DistrictBoundaryGuard>();
            var pose = content.RecoveryPoses[0];
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(ground, content.gameObject.scene);
            ground.transform.position = pose.position + Vector3.down * .34f;
            ground.transform.localScale = new Vector3(8, .2f, 12);
            var coverage = ground.AddComponent<DistrictGroundCoverage>();
            coverage.IgnoreVehicle(car.GetComponentsInChildren<Collider>());
            Physics.SyncTransforms();
            Assert.That(guard.ContainsFootprint(pose.position, Quaternion.identity,
                scene => scene == content.gameObject.scene, true), Is.True);
            ground.transform.position += Vector3.down;
            Physics.SyncTransforms();
            Assert.That(guard.ContainsFootprint(pose.position, Quaternion.identity,
                scene => scene == content.gameObject.scene, true), Is.False, "Visible and physical ground must agree in height.");
            ground.transform.position += Vector3.up;
            Physics.SyncTransforms();
            var outside = pose.position + Vector3.right * 8;
            Assert.That(guard.ContainsFootprint(outside, Quaternion.identity,
                scene => scene == content.gameObject.scene), Is.True, "Fixture's invisible ground still exists.");
            Assert.That(guard.ContainsFootprint(outside, Quaternion.identity,
                scene => scene == content.gameObject.scene, true), Is.False, "Invisible ground cannot extend the playable road.");
            Assert.That(guard.ContainsFootprint(pose.position + Vector3.up * 5, Quaternion.identity,
                scene => scene == content.gameObject.scene), Is.False, "A lower road must not validate a fall into a gap.");
            Object.Destroy(ground);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedLoadsKeepOwnersAndRemoveAllRegistrations()
        {
            int added = 0, removed = 0;
            runtime.DistrictRegistered += _ => added++;
            runtime.DistrictUnregistered += _ => removed++;
            for (int cycle = 0; cycle < 6; cycle++)
            {
                var position = car.Body.position;
                yield return runtime.LoadDistrict(Root + "FixtureB.unity");
                Assert.That(runtime.LastFailure, Is.Null); Assert.That(runtime.DistrictCount, Is.EqualTo(2));
                Assert.That(Vector3.Distance(position, car.Body.position), Is.LessThan(.5f), "Loading remote content must not teleport the player.");
                yield return runtime.LoadDistrict(Root + "FixtureB.unity");
                Assert.That(runtime.DistrictCount, Is.EqualTo(2));
                var b = runtime.LoadedDistricts.Single(d => d.Id == "fixture-b");
                var culling = b.Culling.Single();
                var renderer = b.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>()).Single();
                culling.Refresh(); Assert.That(renderer.forceRenderingOff, Is.True);
                Assert.That(runtime.LoadedDistricts.Sum(d => d.Culling.Sum(c => c.TargetCount)), Is.EqualTo(2));
                yield return runtime.UnloadDistrict("fixture-b");
                Assert.That(runtime.LastFailure, Is.Null); Assert.That(runtime.DistrictCount, Is.EqualTo(1));
                Assert.That(b == null && culling == null && renderer == null, Is.True, "Unloaded objects must be destroyed.");
                Assert.That(runtime.LoadedDistricts.Sum(d => d.Culling.Sum(c => c.TargetCount)), Is.EqualTo(1));
                car.ResetToSpawn(); Assert.That(car.Body.position.x, Is.EqualTo(-10000).Within(.05f));
                AssertOwners();
            }
            Assert.That(added, Is.EqualTo(6)); Assert.That(removed, Is.EqualTo(6));
        }

        [UnityTest]
        public IEnumerator InvalidAndDuplicateContentIsRejectedWithoutChangingOwners()
        {
            foreach (string scene in new[] { "FixtureDuplicate", "FixtureUnsupported" })
            {
                yield return runtime.LoadDistrict(Root + scene + ".unity");
                Assert.That(runtime.LastFailure, Is.Not.Null); Assert.That(runtime.DistrictCount, Is.EqualTo(1));
                Assert.That(SceneManager.GetSceneByPath(Root + scene + ".unity").isLoaded, Is.False);
                AssertOwners();
            }
            yield return runtime.LoadDistrict(Root + "Absent.unity");
            Assert.That(runtime.LastFailure, Does.Contain("absent")); AssertOwners();
        }

        [UnityTest]
        public IEnumerator SupportingUnloadRequiresRetainedRecoveryAndPreservesPause()
        {
            yield return runtime.UnloadDistrict("fixture-a");
            Assert.That(runtime.LastFailure, Does.Contain("last"));
            yield return runtime.LoadDistrict(Root + "FixtureB.unity");
            yield return runtime.UnloadDistrict("fixture-a");
            Assert.That(runtime.LastFailure, Does.Contain("Supporting")); Assert.That(runtime.DistrictCount, Is.EqualTo(2));
            Time.timeScale = 0;
            yield return runtime.UnloadDistrict("fixture-a", "fixture-b");
            Assert.That(runtime.LastFailure, Is.Null); Assert.That(Time.timeScale, Is.Zero);
            Assert.That(car.Body.position.x, Is.EqualTo(-10200).Within(.05f));
            Assert.That(car.Body.linearVelocity.magnitude, Is.LessThan(.05f)); AssertOwners();
            Time.timeScale = 1;
            for (int step = 0; step < 120; step++) yield return new WaitForFixedUpdate();
            Assert.That(car.GetComponentsInChildren<WheelCollider>().Count(w => w.GetGroundHit(out _)), Is.EqualTo(4));
            car.Body.position = new Vector3(-10200, -100, 0);
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(car.Body.position.y, Is.GreaterThan(-1));
            car.ResetToSpawn(); Assert.That(car.Body.position.x, Is.EqualTo(-10200).Within(.05f));
            Assert.That(runtime.DistrictCount, Is.EqualTo(1)); AssertOwners();
        }

        [UnityTest]
        public IEnumerator AirborneVehicleStillProtectsItsSupportingDistrict()
        {
            yield return runtime.LoadDistrict(Root + "FixtureB.unity");
            car.Body.position = new Vector3(-10000, 30, 0); Physics.SyncTransforms();
            yield return runtime.UnloadDistrict("fixture-a");
            Assert.That(runtime.LastFailure, Does.Contain("Supporting"));
            Assert.That(runtime.DistrictCount, Is.EqualTo(2)); AssertOwners();
        }

        [UnityTest]
        public IEnumerator RetainedRecoveryMustStillHaveCollisionAtUnloadTime()
        {
            yield return runtime.LoadDistrict(Root + "FixtureB.unity");
            var b = runtime.LoadedDistricts.Single(d => d.Id == "fixture-b");
            foreach (var root in b.gameObject.scene.GetRootGameObjects())
                foreach (var collider in root.GetComponentsInChildren<Collider>()) collider.enabled = false;
            yield return runtime.UnloadDistrict("fixture-a", "fixture-b");
            Assert.That(runtime.LastFailure, Does.Contain("safe recovery"));
            Assert.That(runtime.DistrictCount, Is.EqualTo(2)); Assert.That(car.Body.position.x, Is.EqualTo(-10000).Within(1)); AssertOwners();
        }

        [UnityTest]
        public IEnumerator RealDowntownReloadsAndSupportsDrivingWithoutRecreatingGameplay()
            => CheckDowntownLifetime(Root + "DowntownContent.unity");

        [UnityTest]
        public IEnumerator ArtDirectionDowntownReloadsAndSupportsDrivingWithoutRecreatingGameplay()
            => CheckDowntownLifetime(ArtDirectionContent);

        private IEnumerator CheckDowntownLifetime(string content)
        {
            if (!File.Exists(content)) Assert.Ignore("Generate the private art-direction content before this variant's qualification.");
            for (int cycle = 0; cycle < 2; cycle++)
            {
                yield return runtime.LoadDistrict(content);
                Assert.That(runtime.LastFailure, Is.Null); AssertOwners();
                yield return runtime.UnloadDistrict("fixture-a", "downtown");
                Assert.That(runtime.LastFailure, Is.Null);
                var downtown = runtime.LoadedDistricts.Single(d => d.Id == "downtown");
                var lods = downtown.gameObject.scene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<NfsWorldMeshLods>()).SingleOrDefault();
                if (content == ArtDirectionContent)
                {
                    Assert.That(lods, Is.Not.Null);
                    Assert.That(lods.TargetsBelongTo(downtown.gameObject.scene), Is.True);
                    var entries = new SerializedObject(lods).FindProperty("entries");
                    Assert.That(entries.arraySize, Is.EqualTo(lods.TargetCount));
                    for (int index = 0; index < entries.arraySize; index++)
                    {
                        var filter = (MeshFilter)entries.GetArrayElementAtIndex(index).FindPropertyRelative("target").objectReferenceValue;
                        Assert.That(GameObjectUtility.GetStaticEditorFlags(filter.gameObject) &
                            StaticEditorFlags.BatchingStatic, Is.EqualTo((StaticEditorFlags)0),
                            "Distance meshes must remain outside Unity's build-time static batches.");
                    }
                }
                Assert.That(downtown.Connections.Length, Is.EqualTo(6));
                Assert.That(downtown.Connections.All(c => !c.seamVerified && c.closure.activeInHierarchy), Is.True);
                for (int step = 0; step < 120; step++) yield return new WaitForFixedUpdate();
                Assert.That(car.GetComponentsInChildren<WheelCollider>().Count(w => w.GetGroundHit(out var h) &&
                    h.collider.gameObject.scene == downtown.gameObject.scene), Is.EqualTo(4));
                input.enabled = false;
                var start = car.Body.position; car.SetCommand(new VehicleCommand(1, 0, 0, false));
                for (int step = 0; step < 240; step++)
                { yield return new WaitForFixedUpdate(); Assert.That(car.Body.position.y, Is.GreaterThan(start.y - 2)); }
                Assert.That(Vector3.Distance(start, car.Body.position), Is.GreaterThan(4));
                input.enabled = true;
                yield return runtime.LoadDistrict(Root + "FixtureA.unity");
                var stale = downtown.Culling;
                yield return runtime.UnloadDistrict("downtown", "fixture-a");
                Assert.That(runtime.LastFailure, Is.Null); Assert.That(stale.All(c => c == null), Is.True);
                Assert.That(lods == null, Is.True, "Visual LOD ownership must end with district unload.");
                Assert.That(runtime.DistrictCount, Is.EqualTo(1)); AssertOwners();
            }
        }
    }

    // Apply a collision-like velocity change after the guard's predictive FixedUpdate.
    [DefaultExecutionOrder(200)]
    public sealed class LateBoundaryImpulse : MonoBehaviour
    {
        public Rigidbody Body;
        private void FixedUpdate()
        { if (Body != null) Body.linearVelocity = new Vector3(18,Body.linearVelocity.y,10); }
    }
}
#endif
