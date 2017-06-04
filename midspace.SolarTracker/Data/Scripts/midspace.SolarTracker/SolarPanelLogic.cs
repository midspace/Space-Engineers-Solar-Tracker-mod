namespace midspace.SolarTracker
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    using Sandbox.Common.ObjectBuilders;
    using Sandbox.Definitions;
    using Sandbox.ModAPI;
    using VRage.Game.Components;
    using VRage.Game.ModAPI;
    using VRage.ModAPI;
    using VRage.ObjectBuilders;
    using VRageMath;
    using IMySolarPanel = SpaceEngineers.Game.ModAPI.Ingame.IMySolarPanel;

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_SolarPanel), false)]
    public class SolarPanelLogic : MyGameLogicComponent
    {
        #region fields

        private MyObjectBuilder_EntityBase _objectBuilder;
        private bool _isInitilized;
        private bool _ignoreNameChange;
        private bool _autoRotate;
        private IMySolarPanel _solarPanelEntity;
        //private string _info = "";
        //private IMyMotorStator motorBase1;
        private IMyMotorStator _motorBase2;

        #endregion

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            _objectBuilder = objectBuilder;
            this.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            this.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;

            if (!_isInitilized)
            {
                // Use this space to hook up events. NOT TO PROCESS ANYTHING.
                _isInitilized = true;

                _solarPanelEntity = (IMySolarPanel)Entity;
                ((Sandbox.ModAPI.IMyTerminalBlock)_solarPanelEntity).CustomNameChanged += _solarPanelEntity_CustomNameChanged;
            }
        }

        public override void Close()
        {
            if (_isInitilized)
            {
                ((Sandbox.ModAPI.IMyTerminalBlock)_solarPanelEntity).CustomNameChanged -= _solarPanelEntity_CustomNameChanged;
            }
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return _objectBuilder;
        }

        void _solarPanelEntity_CustomNameChanged(IMyTerminalBlock obj)
        {
            if (!_ignoreNameChange)
            {
                SetupCustomTrackingProperties();
            }
        }

        public override void MarkForClose()
        {
        }

        public override void UpdateBeforeSimulation()
        {
        }

        public override void UpdateAfterSimulation()
        {
        }

        public override void UpdateBeforeSimulation10()
        {
        }

        public override void UpdateAfterSimulation10()
        {
        }

        public override void UpdateBeforeSimulation100()
        {
            var terminalEntity = (IMyTerminalBlock)Entity;

            if (string.IsNullOrEmpty(terminalEntity.CustomName) || !terminalEntity.ShowOnHUD)
                return;

            try
            {
                if (terminalEntity.CustomName.IndexOf("Az:", StringComparison.InvariantCultureIgnoreCase) >= 0
                    || terminalEntity.CustomName.IndexOf("El:", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    var definitionPanelOrientation = new Vector3(0, 0, -1);

                    // Need to rotate the worldmatrix to match the definition of the solar panel.
                    //Matrix matrix = Entity.WorldMatrix;
                    //Matrix matrix2 = Matrix.CreateFromDir(matrix.Forward, matrix.Up);
                    //var fix = Vector3.Normalize(Vector3.Transform(definitionPanelOrientation, matrix.GetOrientation()));
                    //float num = matrix.Forward.Dot(fix);

                    var ang = GetRotationAngle(Entity.WorldMatrix, Support.GetSunDirection());
                    //MyAPIGateway.Utilities.ShowMessage("Panel offset", String.Format("{0} {1}", ang.X, ang.Y));

                    _ignoreNameChange = true;

                    var match = Regex.Match(terminalEntity.CustomName, @"(?<A>(.*\s{1,}|^)Az:)(?<B>[+-]?((\d+(\.\d*)?)|(\.\d+)))(?<C>(\s{1,}.*|$))", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        terminalEntity.CustomName = string.Format("{0}{1:0.00}{2}", match.Groups["A"].Value, ang.Y, match.Groups["C"].Value);
                    }

                    match = Regex.Match(terminalEntity.CustomName, @"(?<A>(.*\s{1,}|^)El:)(?<B>[+-]?((\d+(\.\d*)?)|(\.\d+)))(?<C>(\s{1,}.*|$))", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        terminalEntity.CustomName = string.Format("{0}{1:0.00}{2}", match.Groups["A"].Value, ang.X, match.Groups["C"].Value);
                    }

                    _ignoreNameChange = false;
                }
            }
            catch (Exception ex)
            {
                var message = ex.Message.Replace("\r", " ").Replace("\n", " ");
                message = message.Substring(0, Math.Min(message.Length, 50));
                MyAPIGateway.Utilities.ShowMessage("Error", String.Format("{0}", message));
            }
        }

        public override void UpdateAfterSimulation100()
        {
        }

        public override void UpdateOnceBeforeFrame()
        {
            // Entity CustomName hasn't been populated in the Init, and this is the only oppurtunity to grab the value first time.
            SetupCustomTrackingProperties();
        }

        private void SetupCustomTrackingProperties()
        {
            if (Entity == null)
                return;

            //MyAPIGateway.Utilities.ShowMessage("step", "1");

            var solarPanelEntity = (IMySolarPanel)Entity;
            var name = solarPanelEntity.CustomName;
            if (!string.IsNullOrEmpty(name))
            {
                _autoRotate = Regex.Match(name, @"(\s{1,}|^)/AR(\s{1,}|$)", RegexOptions.IgnoreCase).Success;
            }
            else
            {
                _autoRotate = false;
            }

            //MyAPIGateway.Utilities.ShowMessage("step", "2");
            //MyAPIGateway.Utilities.ShowMessage("Name", "Setup executing");

            if (_autoRotate)
            {
                var blocks = new List<IMySlimBlock>();
                ((IMyCubeGrid)Entity.Parent).GetBlocks(blocks, f => f.FatBlock != null
                    && (f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_MotorAdvancedRotor)
                    || f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_MotorRotor)
                    || f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_MotorAdvancedStator)
                    || f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_MotorStator)
                    || f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_MotorBase)));

                //_info = blocks.Count.ToString();
                //MyAPIGateway.Utilities.ShowMessage("step", "3");

                if (blocks.Count != 1)
                    return;

                if (blocks[0].FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_MotorAdvancedRotor)
                    || blocks[0].FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_MotorRotor))
                {
                    IMyMotorRotor motorRotor = blocks[0].FatBlock as IMyMotorRotor;

                    if (motorRotor == null || motorRotor.Base == null)
                        return;
                    _motorBase2 = (IMyMotorStator)motorRotor;
                }
                else
                {
                    _motorBase2 = (IMyMotorStator)blocks[0];
                }

                //MyAPIGateway.Utilities.ShowMessage("step", "4");

                var definition = MyDefinitionManager.Static.GetCubeBlockDefinition(_motorBase2.BlockDefinition);
                var motorStatorDefinition = definition as MyMotorStatorDefinition;

                if (motorStatorDefinition == null)
                    return;
            }
        }

        private static Vector2 GetRotationAngle(MatrixD itemMatrix, Vector3D targetVector)
        {
            targetVector = Vector3D.Normalize(targetVector);
            // http://stackoverflow.com/questions/10967130/how-to-calculate-azimut-elevation-relative-to-a-camera-direction-of-view-in-3d
            // rotate so the camera is pointing straight down the z axis
            // (this is essentially a matrix multiplication)
            var obj = new Vector3D(Vector3D.Dot(targetVector, itemMatrix.Right), Vector3D.Dot(targetVector, itemMatrix.Up), Vector3D.Dot(targetVector, itemMatrix.Forward));
            var azimuth = Math.Atan2(obj.X, obj.Z);

            var proj = new Vector3D(obj.X, 0, obj.Z);
            var nrml = Vector3D.Dot(obj, proj);

            var elevation = Math.Acos(nrml);
            if (obj.Y < 0)
                elevation = -elevation;

            if (double.IsNaN(azimuth)) azimuth = 0;
            if (double.IsNaN(elevation)) elevation = 0;

            // Roll is not provided, as target is merely a direction.
            return new Vector2((float)(azimuth * 180 / Math.PI), (float)(elevation * 180 / Math.PI));
        }
    }
}