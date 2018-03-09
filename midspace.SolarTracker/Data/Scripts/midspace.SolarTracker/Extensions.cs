namespace midspace.SolarTracker
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Sandbox.Common.ObjectBuilders;
    using Sandbox.ModAPI;
    using VRage.Game.ModAPI;
    using VRage.ModAPI;

    public static class Extensions
    {
        /// <summary>
        /// Find all grids attached to the specified grid, either by piston or rotor.
        /// This will iterate through all attached grids, until all are found.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns>A list of all attached grids, including the original.</returns>
        public static List<IMyCubeGrid> GetAttachedGrids(this IMyEntity entity)
        {
            return GetAttachedGrids(entity as IMyCubeGrid);
        }

        /// <summary>
        /// Find all grids attached to the specified grid, either by piston or rotor.
        /// This will iterate through all attached grids, until all are found.
        /// </summary>
        /// <param name="cubeGrid"></param>
        /// <returns>A list of all attached grids, including the original.</returns>
        public static List<IMyCubeGrid> GetAttachedGrids(this IMyCubeGrid cubeGrid)
        {
            if (cubeGrid == null)
                return new List<IMyCubeGrid>();

            var results = new List<IMyCubeGrid> { cubeGrid };
            GetAttachedGrids(cubeGrid, ref results);
            return results;
        }

        private static void GetAttachedGrids(IMyCubeGrid cubeGrid, ref List<IMyCubeGrid> results)
        {
            if (cubeGrid == null)
                return;

            var blocks = new List<IMySlimBlock>();
            cubeGrid.GetBlocks(blocks, b => b != null && b.FatBlock != null && !b.FatBlock.BlockDefinition.TypeId.IsNull);

            foreach (var block in blocks)
            {
                //MyAPIGateway.Utilities.ShowMessage("Block", string.Format("{0}", block.FatBlock.BlockDefinition.TypeId));

                if (block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_MotorAdvancedStator) ||
                    block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_MotorStator) ||
                    block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_MotorSuspension) ||
                    block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_MotorBase))
                {
                    // The MotorStator which inherits from MotorBase.
                    IMyMotorBase motorBase = block.FatBlock as IMyMotorBase;
                    if (motorBase == null || motorBase.Top == null)
                        continue;

                    IMyCubeGrid entityParent = motorBase.TopGrid;
                    if (entityParent == null)
                        continue;
                    if (!results.Any(e => e.EntityId == entityParent.EntityId))
                    {
                        results.Add(entityParent);
                        GetAttachedGrids(entityParent, ref results);
                    }
                }
                else if (block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_MotorAdvancedRotor) ||
                    block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_MotorRotor) ||
                    block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_RealWheel) ||
                    block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_Wheel))
                {
                    // The Rotor Part.
                    IMyMotorRotor motorRotor = block.FatBlock as IMyMotorRotor;
                    if (motorRotor == null || motorRotor.Base == null)
                        continue;

                    IMyCubeGrid entityParent = motorRotor.Base.CubeGrid;
                    if (!results.Any(e => e.EntityId == entityParent.EntityId))
                    {
                        results.Add(entityParent);
                        GetAttachedGrids(entityParent, ref results);
                    }
                }
                else if (block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_PistonTop))
                {
                    // The Piston Top.
                    IMyPistonTop pistonTop = block.FatBlock as IMyPistonTop;
                    if (pistonTop == null || pistonTop.Piston == null)
                        continue;

                    IMyCubeGrid entityParent = pistonTop.Piston.CubeGrid;
                    if (!results.Any(e => e.EntityId == entityParent.EntityId))
                    {
                        results.Add(entityParent);
                        GetAttachedGrids(entityParent, ref results);
                    }
                }
                else if (block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_ExtendedPistonBase) ||
                    block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_PistonBase))
                {
                    IMyPistonBase pistonBase = block.FatBlock as IMyPistonBase;
                    if (pistonBase == null || pistonBase.Top == null)
                        continue;

                    IMyCubeGrid entityParent = pistonBase.TopGrid;
                    if (entityParent == null)
                        continue;
                    if (!results.Any(e => e.EntityId == entityParent.EntityId))
                    {
                        results.Add(entityParent);
                        GetAttachedGrids(entityParent, ref results);
                    }
                }
            }
        }

        /// <summary>
        /// Time elapsed since the start of the game.
        /// This is saved in checkpoint, instead of GameDateTime.
        /// </summary>
        /// <remarks>Copied from Sandbox.Game.World.MySession</remarks>
        public static TimeSpan ElapsedGameTime(this IMySession session)
        {
            return MyAPIGateway.Session.GameDateTime - new DateTime(2081, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }
    }
}