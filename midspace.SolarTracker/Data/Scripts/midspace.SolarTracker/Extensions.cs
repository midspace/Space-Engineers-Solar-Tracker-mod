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
                    var motorBase = block.GetObjectBuilder() as MyObjectBuilder_MotorBase;
                    if (motorBase == null || !motorBase.RotorEntityId.HasValue || motorBase.RotorEntityId.Value == 0 || !MyAPIGateway.Entities.EntityExists(motorBase.RotorEntityId.Value))
                        continue;
                    var entityParent = MyAPIGateway.Entities.GetEntityById(motorBase.RotorEntityId.Value).Parent as IMyCubeGrid;
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
                    var motorCube = Support.FindRotorBase(block.FatBlock.EntityId);
                    if (motorCube == null)
                        continue;
                    var entityParent = (IMyCubeGrid)motorCube.Parent;
                    if (!results.Any(e => e.EntityId == entityParent.EntityId))
                    {
                        results.Add(entityParent);
                        GetAttachedGrids(entityParent, ref results);
                    }
                }
                else if (block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_PistonTop))
                {
                    // The Piston Top.
                    var pistonCube = Support.FindPistonBase(block.FatBlock.EntityId);
                    if (pistonCube == null)
                        continue;
                    var entityParent = (IMyCubeGrid)pistonCube.Parent;
                    if (!results.Any(e => e.EntityId == entityParent.EntityId))
                    {
                        results.Add(entityParent);
                        GetAttachedGrids(entityParent, ref results);
                    }
                }
                else if (block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_ExtendedPistonBase) ||
                    block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_PistonBase))
                {
                    var pistonBase = block.GetObjectBuilder() as MyObjectBuilder_PistonBase;
                    if (pistonBase == null || pistonBase.TopBlockId == 0 || !MyAPIGateway.Entities.EntityExists(pistonBase.TopBlockId))
                        continue;
                    var entityParent = MyAPIGateway.Entities.GetEntityById(pistonBase.TopBlockId).Parent as IMyCubeGrid;
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
        /// Replicates the Sandbox.ModAPI.Ingame.TerminalBlockExtentions.ApplyAction(), but without the messy namespacing.
        /// </summary>
        /// <param name="terminalBlock"></param>
        /// <param name="actionName"></param>
        public static void ApplyAction(this Sandbox.ModAPI.Ingame.IMyTerminalBlock terminalBlock, string actionName)
        {
            var action = terminalBlock.GetActionWithName(actionName);
            if (action != null)
                action.Apply(terminalBlock);
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