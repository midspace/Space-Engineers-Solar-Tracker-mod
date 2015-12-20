namespace midspace.SolarTracker
{
    using System;
    using System.Collections.Generic;
    using Sandbox.Common.ObjectBuilders;
    using Sandbox.Common.ObjectBuilders.Definitions;
    using Sandbox.Definitions;
    using Sandbox.ModAPI;
    using VRage.ModAPI;
    using VRageMath;

    public static class Support
    {
        private static double _calcFrequency;

        public static IMyCubeBlock FindRotorBase(long entityId, IMyCubeGrid parent = null)
        {
            var entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities, e => e is IMyCubeGrid);

            foreach (var entity in entities)
            {
                var cubeGrid = (IMyCubeGrid)entity;

                if (cubeGrid == null)
                    continue;

                var blocks = new List<IMySlimBlock>();
                cubeGrid.GetBlocks(blocks, block => block != null && block.FatBlock != null &&
                    (block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_MotorAdvancedStator) ||
                    block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_MotorStator) ||
                    block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_MotorSuspension) ||
                    block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_MotorBase)));

                foreach (var block in blocks)
                {
                    var motorBase = block.GetObjectBuilder() as MyObjectBuilder_MotorBase;

                    if (motorBase == null || !motorBase.RotorEntityId.HasValue || motorBase.RotorEntityId.Value == 0 || !MyAPIGateway.Entities.EntityExists(motorBase.RotorEntityId.Value))
                        continue;

                    if (motorBase.RotorEntityId == entityId)
                        return block.FatBlock;
                }
            }

            return null;
        }

        public static IMyCubeBlock FindPistonBase(long entityId, IMyCubeGrid parent = null)
        {
            var entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities, e => e is IMyCubeGrid);

            foreach (var entity in entities)
            {
                var cubeGrid = (IMyCubeGrid)entity;

                if (cubeGrid == null)
                    continue;

                var blocks = new List<IMySlimBlock>();
                cubeGrid.GetBlocks(blocks, block => block != null && block.FatBlock != null &&
                    (block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_PistonBase) ||
                    block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_ExtendedPistonBase)));

                foreach (var block in blocks)
                {
                    var pistonBase = block.GetObjectBuilder() as MyObjectBuilder_PistonBase;

                    if (pistonBase == null || pistonBase.TopBlockId == 0 || !MyAPIGateway.Entities.EntityExists(pistonBase.TopBlockId))
                        continue;

                    if (pistonBase.TopBlockId == entityId)
                        return block.FatBlock;
                }
            }

            return null;
        }

        private static DateTime _lastFetch;
        private static Vector3D _sunDirection;
        private static Vector3D? _environmentSunDirection;
        //private readonly static object fetchLock = new object(); //Lock isn't available in Mod API ;(

        /// <summary>
        /// This is to replace MyDefinitionManager.Static.EnvironmentDefinition.DirectionToSun, as the property has been moved to ...
        /// MyDefinitionManager.Static.EnvironmentDefinition.SunProperties.B6A7B39D90F03EC39812DF6BCEAC7DFD
        /// which is obfuscated and not accessible as a result.
        /// </summary>
        /// <returns></returns>
        public static Vector3D GetSunDirection()
        {
            // 820 milliseconds lead time in calculation, to offset for fast sun movement.
            const double LeadTime = 0.820 / 60;


            if (_calcFrequency == 0)
            {
                // Try to limit the number of updates, to every 720 updates in the orbit (every half a degree), with a minimum update frequency of 500 milliseconds.
                // This should make anything longer than a 6 minute orbit more efficient.
                // However, the timing for the Sun in the client can get out of sync with the Server, and this could circumvent keeping that sync.
                _calcFrequency = Math.Max(MyAPIGateway.Session.SessionSettings.SunRotationIntervalMinutes / 0.012d, 500);
            }

            var fetch = DateTime.Now;
            if ((fetch - _lastFetch).TotalMilliseconds > _calcFrequency)
            {
                _lastFetch = fetch;
                if (MyAPIGateway.Session.SessionSettings.EnableSunRotation)
                {
                    //if (MyAPIGateway.Utilities.IsDedicated)
                    //{
                    //var ob = (MyObjectBuilder_EnvironmentDefinition)MyDefinitionManager.Static.EnvironmentDefinition.GetObjectBuilder(); // GetObjectBuilder() is no longer deserializing Sun properties in EnvironmentDefinition since 1.105.
                    //Vector3D sunDirection = -(Vector3)ob.SunDirection;

                    if (!_environmentSunDirection.HasValue)
                    {
                        var environment = MyAPIGateway.Session.GetSector().Environment;
                        Vector3D direction;
                        Vector3D.CreateFromAzimuthAndElevation(environment.SunAzimuth, environment.SunElevation, out direction);
                        _environmentSunDirection = -direction;
                    }

                    Vector3D sunDirection = _environmentSunDirection.Value;

                    // copied from Sandbox.Game.Gui.MyGuiScreenGamePlay.Draw()
                    float angle = MathHelper.TwoPi * (float)((MyAPIGateway.Session.ElapsedGameTime().TotalMinutes + LeadTime) / MyAPIGateway.Session.SessionSettings.SunRotationIntervalMinutes);
                    float originalSunCosAngle = Math.Abs(Vector3.Dot(sunDirection, Vector3.Up));
                    Vector3D sunRotationAxis = Vector3D.Cross(Vector3D.Cross(sunDirection, originalSunCosAngle > 0.95f ? Vector3D.Left : Vector3D.Up), sunDirection);
                    sunDirection = Vector3.Normalize(Vector3.Transform(sunDirection, Matrix.CreateFromAxisAngle(sunRotationAxis, angle)));
                    _sunDirection = -sunDirection;
                    //}
                    //else
                    //{
                    //    var environment = MyAPIGateway.Session.GetSector().Environment;  // GetSector() is no longer deserializing Sun properties in Environment since 1.105.
                    //    Vector3D.CreateFromAzimuthAndElevation(environment.SunAzimuth, environment.SunElevation, out _sunDirection);
                    //}
                }
                else
                {
                    // no calculation invovled.
                    var ob = (MyObjectBuilder_EnvironmentDefinition)MyDefinitionManager.Static.EnvironmentDefinition.GetObjectBuilder();
                    _sunDirection = new Vector3D(ob.SunDirection);
                }
            }
            return _sunDirection;
        }
    }
}