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
                    block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_PistonBase));

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

            var fetch = DateTime.Now;
            if ((fetch - _lastFetch).TotalSeconds > 0.5)
            {
                _lastFetch = fetch;
                if (MyAPIGateway.Session.SessionSettings.EnableSunRotation)
                {
                    //if (MyAPIGateway.Utilities.IsDedicated)
                    //{
                    //var ob = (MyObjectBuilder_EnvironmentDefinition)MyDefinitionManager.Static.EnvironmentDefinition.GetObjectBuilder(); // GetObjectBuilder() is no longer deserializing Sun properties in EnvironmentDefinition since 1.105.
                    //Vector3D sunDirection = -(Vector3)ob.SunDirection;

                    var environment = MyAPIGateway.Session.GetSector().Environment;
                    Vector3D sunDirection;
                    Vector3D.CreateFromAzimuthAndElevation(environment.SunAzimuth, environment.SunElevation, out sunDirection);
                    sunDirection = -sunDirection;

                    // copied from Sandbox.Game.Gui.MyGuiScreenGamePlay.Draw()
                    float angle = 2.0f * MathHelper.Pi * (float)((MyAPIGateway.Session.ElapsedGameTime().TotalMinutes + LeadTime) / MyAPIGateway.Session.SessionSettings.SunRotationIntervalMinutes);
                    float originalSunCosAngle = Math.Abs(Vector3.Dot(sunDirection, Vector3.Up));
                    Vector3 sunRotationAxis;
                    if (originalSunCosAngle > 0.95f)
                    {
                        // original sun is too close to the poles
                        sunRotationAxis = Vector3.Cross(Vector3.Cross(sunDirection, Vector3.Left), sunDirection);
                    }
                    else
                    {
                        sunRotationAxis = Vector3.Cross(Vector3.Cross(sunDirection, Vector3.Up), sunDirection);
                    }
                    sunDirection = Vector3.Transform(sunDirection, Matrix.CreateFromAxisAngle(sunRotationAxis, angle));
                    sunDirection.Normalize();

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