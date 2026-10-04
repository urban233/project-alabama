using System.Linq;
using Alabama.Editor;
using Alabama.MicroSlice;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using Object = UnityEngine.Object;

namespace Alabama.Tests
{
    public sealed class MicroSliceTests
    {
        [OneTimeSetUp]
        public void EnsureGeneratedDemo()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MicroSliceSetup.ScenePath)==null) MicroSliceSetup.Setup();
        }
        [SetUp]
        public void EmptyScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);

        [Test]
        public void LapRequiresHillArterialAndReturnInOrder()
        {
            var state=new MicroSliceLapState();
            Assert.That(state.Pass(2,true,1),Is.False);
            Assert.That(state.Pass(0,true,2),Is.True);
            Assert.That(state.Pass(3,true,3),Is.False);
            state.Pass(1,true,20);state.Pass(2,true,40);
            Assert.That(state.Pass(0,true,55),Is.False,"Returning early cannot grant a lap.");
            state.Pass(3,true,60);state.Pass(0,true,92);
            Assert.That(state.CompletedLaps,Is.EqualTo(1));
            Assert.That(state.LastLapSeconds,Is.EqualTo(90));
        }

        [Test]
        public void ReverseCrossingAndDuplicateReturnCannotGrantLap()
        {
            var state=new MicroSliceLapState();state.Pass(0,true,0);state.Pass(1,true,20);state.Pass(2,true,40);
            Assert.That(state.Pass(3,false,50),Is.False);
            Assert.That(state.Pass(3,true,60),Is.True);
            Assert.That(state.Pass(3,true,62),Is.False);
            Assert.That(state.Pass(0,false,80),Is.False);
            Assert.That(state.CompletedLaps,Is.Zero);
            state.Pass(0,true,90);Assert.That(state.CompletedLaps,Is.EqualTo(1));
        }

        [Test]
        public void ResetCannotKeepPartialLapCredit()
        {
            var state=new MicroSliceLapState();state.Pass(0,true,0);state.Pass(1,true,20);state.Reset();
            Assert.That(state.Pass(2,true,30),Is.False);
            Assert.That(state.Running,Is.False);Assert.That(state.CompletedLaps,Is.Zero);
        }

        [Test]
        public void CooldownRequiresContinuousHiddenLowSpeedOccupancy()
        {
            var state=new CooldownState();
            state.Tick(10,true,false,0);Assert.That(state.Ready,Is.False);
            state.Tick(10,true,true,6);Assert.That(state.Ready,Is.False);
            state.Tick(2,true,true,0);Assert.That(state.Ready,Is.False);
            state.Tick(1,true,true,0);Assert.That(state.Ready,Is.True);
            state.Tick(.1f,false,true,0);Assert.That(state.Ready,Is.False);
            state.Tick(2,true,true,0);state.Tick(.1f,true,false,0);
            Assert.That(state.HiddenSeconds,Is.Zero);
        }

        [Test]
        public void BreakerProtectsActivatorAndRestoresItsPoseOnRearm()
        {
            var root=new GameObject("Test breaker");
            var structure=root.AddComponent<BreakableStructure>();
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.transform.SetParent(root.transform,false);
            part.transform.localPosition=Vector3.up*5;
            var body=part.AddComponent<Rigidbody>();body.isKinematic=true;
            var car=GameObject.CreatePrimitive(PrimitiveType.Cube);var activator=car.AddComponent<Rigidbody>();activator.mass=1400;
            structure.Configure(new[]{body});structure.Release(activator);
            Assert.That(body.isKinematic,Is.False);
            Assert.That(Physics.GetIgnoreCollision(part.GetComponent<Collider>(),car.GetComponent<Collider>()),Is.True);
            structure.Release(activator);body.transform.localPosition=Vector3.zero;
            structure.Rearm();
            Assert.That(body.isKinematic,Is.True);Assert.That(body.transform.localPosition,Is.EqualTo(Vector3.up*5));
            Assert.That(Physics.GetIgnoreCollision(part.GetComponent<Collider>(),car.GetComponent<Collider>()),Is.False);
            Object.DestroyImmediate(root);Object.DestroyImmediate(car);
        }

        [Test]
        public void LoftClosesWithoutWidthOrBankSeam()
        {
            EditorSceneManager.OpenScene(MicroSliceSetup.ScenePath);
            var road=GameObject.Find("Main circuit");
            var samples=MicroSliceRoadBuilder.SampleRoad(road.GetComponent<SplineContainer>(),road.GetComponent<SplineRoadProfile>());
            Assert.That(MicroSliceRoadBuilder.IsContinuous(samples,true),Is.True);
            Assert.That(samples.Max(s=>s.width),Is.EqualTo(16).Within(.05f));
            Assert.That(samples.Max(s=>s.position.y)-samples.Min(s=>s.position.y),Is.GreaterThan(24));
        }

        [Test]
        public void RoadColliderSupportsCentrelineAcrossCrestsAndClosure()
        {
            EditorSceneManager.OpenScene(MicroSliceSetup.ScenePath);Physics.SyncTransforms();
            var road=GameObject.Find("Main circuit");
            var samples=MicroSliceRoadBuilder.SampleRoad(road.GetComponent<SplineContainer>(),road.GetComponent<SplineRoadProfile>());
            for(int i=0;i<samples.Count;i+=13)
            {
                var p=samples[i].position;
                Assert.That(Physics.Raycast(p+Vector3.up*3,Vector3.down,out var hit,6,1<<LayerMask.NameToLayer("MicroRoad")),Is.True,"Road support missing at sample "+i);
                Assert.That(hit.point.y,Is.EqualTo(p.y).Within(.08f),"Terrain or a gap replaces the loft at sample "+i);
            }
        }

        [Test]
        public void EscapeAlleyAndGarageBayHaveRealVehicleClearance()
        {
            EditorSceneManager.OpenScene(MicroSliceSetup.ScenePath);Physics.SyncTransforms();
            var road=GameObject.Find("Drive-through escape alley");
            var samples=MicroSliceRoadBuilder.SampleRoad(road.GetComponent<SplineContainer>(),road.GetComponent<SplineRoadProfile>());
            int mask=(1<<LayerMask.NameToLayer("MicroStructure"))|(1<<LayerMask.NameToLayer("MicroCurbs"))|(1<<LayerMask.NameToLayer("MicroRoad"));
            for(int i=3;i<samples.Count-2;i+=7)
            {
                var tangent=Vector3.Cross(samples[i].right,samples[i].up);
                Assert.That(Physics.CheckBox(samples[i].position+samples[i].up*.9f,new Vector3(1.05f,.55f,2.2f),
                    Quaternion.LookRotation(tangent,Vector3.up),mask,QueryTriggerInteraction.Ignore),Is.False,"Alley obstruction at sample "+i);
            }
            Assert.That(Physics.CheckBox(new Vector3(-94,25,42),new Vector3(1,.7f,2.2f),Quaternion.identity,mask,QueryTriggerInteraction.Ignore),Is.False,
                "The workshop's open bay must be geometry, not a painted door.");
        }

        [Test]
        public void AlleyLevelsBeforeTheCourtyardFrontEdge()
        {
            EditorSceneManager.OpenScene(MicroSliceSetup.ScenePath);Physics.SyncTransforms();
            var plan=MicroSliceSetup.ReadPlan();var road=GameObject.Find("Drive-through escape alley");
            var samples=MicroSliceRoadBuilder.SampleRoad(road.GetComponent<SplineContainer>(),road.GetComponent<SplineRoadProfile>());
            float front=plan.courtyard.center.z-plan.courtyard.size.z/2;
            var approach=samples.OrderBy(s=>Mathf.Abs(s.position.z-(front-2.2f))).First();
            Assert.That(approach.position.y,Is.GreaterThanOrEqualTo(plan.courtyard.center.y-.15f),
                "The car's front overhang must not hit a raised courtyard face while climbing the alley.");
            Assert.That(Physics.Raycast(approach.position+Vector3.up*3,Vector3.down,out var hit,6,1<<LayerMask.NameToLayer("MicroRoad")),Is.True);
            Assert.That(hit.point.y,Is.EqualTo(approach.position.y).Within(.08f));
        }

        [Test]
        public void AlternateReturnRejoinsUpstreamOfForwardFinish()
        {
            EditorSceneManager.OpenScene(MicroSliceSetup.ScenePath);
            var plan=MicroSliceSetup.ReadPlan();
            var main=GameObject.Find("Main circuit");var alley=GameObject.Find("Drive-through escape alley");var drive=GameObject.Find("Garage entry");
            var samples=MicroSliceSetup.ShortcutCentreline(
                MicroSliceRoadBuilder.SampleRoad(main.GetComponent<SplineContainer>(),main.GetComponent<SplineRoadProfile>()),
                MicroSliceRoadBuilder.SampleRoad(alley.GetComponent<SplineContainer>(),alley.GetComponent<SplineRoadProfile>()),
                MicroSliceRoadBuilder.SampleRoad(drive.GetComponent<SplineContainer>(),drive.GetComponent<SplineRoadProfile>()),plan.gates[0].position);
            Assert.That(Vector3.Distance(samples[0],samples[^1]),Is.LessThan(.01f));
            Assert.That(Vector3.Dot((samples[^1]-samples[^2]).normalized,plan.gates[0].forward.normalized),Is.GreaterThan(.8f),
                "The garage exit must approach the finish in the accepted direction.");
            for(int i=1;i<samples.Length;i++) Assert.That(Vector3.Distance(samples[i-1],samples[i]),Is.GreaterThan(.05f));
            int finishNearby=samples.Count(p=>Vector3.Distance(p,plan.gates[0].position)<4);
            Assert.That(finishNearby,Is.LessThan(9),"The alternate route must not double back across the finish.");
        }

        [Test]
        public void YieldTriggersCoverFenceEndsAndPoleBases()
        {
            EditorSceneManager.OpenScene(MicroSliceSetup.ScenePath);Physics.SyncTransforms();
            foreach(var prop in Object.FindObjectsByType<BreakableStructure>(FindObjectsSortMode.None).Where(p=>p.GetComponent<Rigidbody>()!=null))
            {
                var trigger=prop.GetComponent<BoxCollider>();
                Assert.That(trigger.isTrigger,Is.True);
                foreach(var solid in prop.GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger))
                {
                    Assert.That(trigger.bounds.Contains(solid.bounds.min),Is.True,prop.name+" misses the lower/end collision.");
                    Assert.That(trigger.bounds.Contains(solid.bounds.max),Is.True,prop.name+" misses the upper/end collision.");
                }
            }
        }

        [Test]
        public void SceneContainsEditableVolumesAndSeparateCollisionLayers()
        {
            EditorSceneManager.OpenScene(MicroSliceSetup.ScenePath);
            Assert.That(Object.FindObjectsByType<UnityEngine.ProBuilder.ProBuilderMesh>(FindObjectsSortMode.None).Length,Is.GreaterThanOrEqualTo(8));
            int road=LayerMask.NameToLayer("MicroRoad"),curbs=LayerMask.NameToLayer("MicroCurbs"),props=LayerMask.NameToLayer("MicroBreakable");
            Assert.That(road,Is.GreaterThanOrEqualTo(8));Assert.That(curbs,Is.Not.EqualTo(road));Assert.That(props,Is.Not.EqualTo(curbs));
            Assert.That(Object.FindObjectsByType<BreakableStructure>(FindObjectsSortMode.None).Length,Is.GreaterThan(5));
        }
    }
}
