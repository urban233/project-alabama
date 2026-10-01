using System;
using Alabama.Bootstrap;
using Alabama.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alabama.Tests
{
    public sealed class FoundationTests
    {
        [TestCase("screenWidth", 0)]
        [TestCase("screenHeight", -1)]
        [TestCase("targetFrameRate", 0)]
        [TestCase("physicsRate", 29)]
        [TestCase("physicsRate", 241)]
        public void InvalidConfigurationIsRejected(string field, int value)
        {
            var settings = ScriptableObject.CreateInstance<DemoSettings>();
            try
            {
                var serialized = new SerializedObject(settings);
                serialized.FindProperty(field).intValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(settings.Validate);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void SavedProjectAndCalibrationImportPassVerification()
        {
            Assert.DoesNotThrow(ProjectFoundation.Verify);
        }
    }
}
