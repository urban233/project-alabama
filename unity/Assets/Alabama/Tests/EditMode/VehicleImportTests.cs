using Alabama.Editor;
using NUnit.Framework;

namespace Alabama.Tests.EditMode
{
    public sealed class VehicleImportTests
    {
        [Test]
        public void ExportPreservesVehicleScaleWheelAxesMaterialsAndBudget()
        {
            Assert.DoesNotThrow(VehicleAssetSetup.Verify);
        }

        [Test]
        public void ReplacementWheelGeometryMatchesDrivingReferencesAndPhysics()
        {
            Assert.DoesNotThrow(SuppliedCarSetup.Verify);
        }
    }
}
