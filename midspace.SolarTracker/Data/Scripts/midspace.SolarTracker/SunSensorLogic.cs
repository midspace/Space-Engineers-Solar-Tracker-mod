namespace midspace.SolarTracker
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    using Sandbox.Common.Components;
    using Sandbox.Common.ObjectBuilders;
    using Sandbox.Definitions;
    using Sandbox.ModAPI;
    using Sandbox.ModAPI.Interfaces;
    using VRage.Components;
    using VRage.ModAPI;
    using VRage.ObjectBuilders;
    using VRageMath;

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_SensorBlock))]
    public class SunSensorLogic : MyGameLogicComponent
    {
        enum RotateDirections { Unknown, RollPositive, RollNegative, PitchPositive, PitchNegative, YawPositive, YawNegative };

        #region fields

        private MyObjectBuilder_EntityBase _objectBuilder;
        private bool _isInitialized;
        private bool _isIsValid;
        private bool _ignoreNameChange;
        private bool _autoTrackOn;
        private IMySensorBlock _sunSensorEntity;
        private string _exceptionInfo = "";
        private List<IMyEntity> _attachedRotors = new List<IMyEntity>();
        private List<RotateDirections> _rotorDirections = new List<RotateDirections>();
        private bool Debug;
        private int _frameCounter;

        #endregion

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            _objectBuilder = objectBuilder;
            Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        private void Initilize()
        {
            _isInitialized = true; // Set this first to block any other calls from UpdateAfterSimulation().
            _sunSensorEntity = (IMySensorBlock)Entity;
            _isIsValid = (_sunSensorEntity.BlockDefinition.SubtypeName.Equals("SmallSunSensor", StringComparison.InvariantCultureIgnoreCase)
                || _sunSensorEntity.BlockDefinition.SubtypeName.Equals("LargeSunSensor", StringComparison.InvariantCultureIgnoreCase));

            if (_isIsValid)
                ((IMyTerminalBlock)_sunSensorEntity).CustomNameChanged += _iMyTerminalBlock_CustomNameChanged;
        }

        public override void Close()
        {
            if (_isInitialized && _isIsValid)
                ((IMyTerminalBlock)_sunSensorEntity).CustomNameChanged -= _iMyTerminalBlock_CustomNameChanged;
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return _objectBuilder;
        }

        void _iMyTerminalBlock_CustomNameChanged(IMyTerminalBlock obj)
        {
            WriteDebug("Name Changed", "IsValid = {0}", _isIsValid);

            if (!_isIsValid)
                return;

            if (!_ignoreNameChange)
            {
                try
                {
                    SetupCustomTrackingProperties();
                }
                catch (Exception ex)
                {
                    _exceptionInfo = ex.Message;
                }
            }
        }

        public override void MarkForClose()
        {
        }

        public override void UpdateAfterSimulation()
        {
            if (!_isInitialized)
                return;

            _frameCounter++;
            if (_frameCounter < 30)
                return;

            _frameCounter = 0;

            Debug = ((IMyTerminalBlock)Entity).IsWorking && ((IMyTerminalBlock)Entity).ShowOnHUD;

            if (!string.IsNullOrEmpty(_exceptionInfo))
                WriteDebug("Info", _exceptionInfo);

            if (!_isIsValid)
                return;

            if (!_autoTrackOn)
                return;

            var terminalEntity = (IMyTerminalBlock)Entity;

            if (string.IsNullOrEmpty(terminalEntity.CustomName) || !terminalEntity.IsWorking || !terminalEntity.IsFunctional)
                return;


            Vector2 ang = Vector2.Zero;
            // TODO: run as background, if I can find a suitable world to test it on FIRST!

            MyAPIGateway.Parallel.Start(delegate ()
            // Background processing occurs within this block.
            {
                try
                {
                    var sunDirection = Support.GetSunDirection();
                    ang = GetRotationAngle(Entity.WorldMatrix, sunDirection);
                }
                catch (Exception ex)
                {
                }

            }, delegate ()
            // when the background processing is finished, this block will run foreground.
            {
                try
                {


                    // The Sun doesn't turn the a Dedicated Server. :(
                    // None of these give anything but a static position on a Server.

                    //var environment = MyAPIGateway.Session.GetSector().Environment;
                    //Vector3D sunDirection1;
                    //Vector3D.CreateFromAzimuthAndElevation(environment.SunAzimuth, environment.SunElevation, out sunDirection1);
                    //WriteDebug("SunDirection1", "{0} {1} {2}", sunDirection.X, sunDirection.Y, sunDirection.Z);

                    //var ed = ((MyObjectBuilder_EnvironmentDefinition)MyDefinitionManager.Static.EnvironmentDefinition.GetObjectBuilder());
                    //WriteDebug("SunDirection2", "{0} {1} {2}", ed.SunDirection.X, ed.SunDirection.Y, ed.SunDirection.Z);

                    //environment = MyAPIGateway.Session.GetWorld().Sector.Environment;
                    //Vector3D.CreateFromAzimuthAndElevation(environment.SunAzimuth, environment.SunElevation, out sunDirection1);
                    //WriteDebug("SunDirection3", "{0} {1} {2}", sunDirection.X, sunDirection.Y, sunDirection.Z);

                    //MyAPIGateway.Multiplayer.
                    //MyAPIGateway.Utilities.ConfigDedicated.SessionSettings.
                    //MyAPIGateway.Session.GetCheckpoint("null").;


                    // Check Ownership.
                    var block = (IMyCubeBlock)Entity;

                    //WriteDebug("Panel offset", "{0} {1}", ang.X, ang.Y);

                    // ang.Y Azimuth  Yaw.
                    // any.X Elevation  Pitch.

                    if (_attachedRotors.Count >= 2)
                    {
                        var rotorBase = _attachedRotors[0] as IMyTerminalBlock;
                        if (rotorBase == null)
                            rotorBase = _attachedRotors[1] as IMyTerminalBlock;

                        if (rotorBase != null && !rotorBase.Closed && rotorBase.HasPlayerAccess(block.OwnerId))
                        {
                            WriteDebug("Turn", "'{0}'", rotorBase.CustomName);
                            TurnRotor(_attachedRotors[0], _attachedRotors[1], ang, _rotorDirections[0]);
                        }
                    }
                    if (_attachedRotors.Count >= 3)
                    {
                        var rotorBase = _attachedRotors[2] as IMyTerminalBlock;
                        if (rotorBase == null)
                            rotorBase = _attachedRotors[3] as IMyTerminalBlock;

                        if (rotorBase != null && !rotorBase.Closed && rotorBase.HasPlayerAccess(block.OwnerId))
                        {
                            WriteDebug("Turn", "'{0}'", rotorBase.CustomName);
                            TurnRotor(_attachedRotors[2], _attachedRotors[3], ang, _rotorDirections[1]);
                        }
                    }
                }
                catch (Exception ex)
                {
                    var message = ex.Message.Replace("\r", " ").Replace("\n", " ");
                    message = message.Substring(0, Math.Min(message.Length, 100));
                    MyAPIGateway.Utilities.ShowMessage("Error", String.Format("{0}", message));
                }
            });



        }

        public override void UpdateAfterSimulation10()
        {
        }

        private void TurnRotor(IMyEntity motorpart1, IMyEntity motorpart2, Vector2 heading, RotateDirections rotateDirection)
        {
            var motorBase = motorpart1 as IMyMotorStator;
            if (motorBase == null)
                motorBase = (IMyMotorStator)motorpart2;
            if (motorBase == null)
                return;

            //float velocity = 0f;
            //if (rotateDirection == RotateDirections.YawPositive) velocity = GetTurnVelocity(heading.X);
            //if (rotateDirection == RotateDirections.YawNegative) velocity = -1 * GetTurnVelocity(heading.X);
            //if (rotateDirection == RotateDirections.PitchPositive) velocity = -1 * GetTurnVelocity(heading.Y);
            //if (rotateDirection == RotateDirections.PitchNegative) velocity = GetTurnVelocity(heading.Y);

            float turnAngle = 0;
            if (rotateDirection == RotateDirections.YawPositive) turnAngle = heading.X;
            if (rotateDirection == RotateDirections.YawNegative) turnAngle = -1 * heading.X;
            if (rotateDirection == RotateDirections.PitchPositive) turnAngle = -1 * heading.Y;
            if (rotateDirection == RotateDirections.PitchNegative) turnAngle = heading.Y;
            float velocity = GetTurnVelocity(turnAngle);

            //_exceptionInfo = string.Format("{0} {1} / El:{2:N} Az:{3:N} ", _rotorDirections[0], velocity, turnAngle.X, turnAngle.Y);

            WriteDebug("Turn", "Rotor Velocity {0}", velocity);

            if (velocity == 0)
            {
                var action = motorBase.GetActionWithName("OnOff_Off");
                action.Apply(motorBase);
            }
            else
            {
                // TODO: check rotor limit aginst turnAngle;

                motorBase.SetValue("Velocity", velocity);
                var action = motorBase.GetActionWithName("OnOff_On");
                action.Apply(motorBase);
            }
        }

        private float GetTurnVelocity(float angle)
        {
            return (angle > +0.1f || angle < -0.1f) ? Math.Min(angle / 5f, 10) : 0f;

            //if (angle > +5.0f) return 1.2f;
            //else if (angle < -5.0f) return -1.2f;
            //else if (angle > +0.5f) return +0.1f;
            //else if (angle < -0.5f) return -0.1f;
            //else if (angle > +0.1f) return +0.02f;
            //else if (angle < -0.1f) return -0.02f;
            //return 0;
        }

        public override void UpdateOnceBeforeFrame()
        {
            // Single player or hosted.
            if (!_isInitialized && MyAPIGateway.Session != null && MyAPIGateway.Session.Player != null && MyAPIGateway.Multiplayer.IsServer && !MyAPIGateway.Utilities.IsDedicated)
                Initilize();

            // Dedicated server.
            if (!_isInitialized && MyAPIGateway.Utilities != null && MyAPIGateway.Multiplayer != null
                && MyAPIGateway.Session != null && MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Multiplayer.IsServer)
                Initilize();

            if (!_isIsValid)
                return;

            // Entity CustomName hasn't been populated in the Init, and this is the only oppurtunity to grab the value first time.
            SetupCustomTrackingProperties();
            ResetSunSensor(Entity as Sandbox.ModAPI.IMySensorBlock);
        }

        private void ResetSunSensor(Sandbox.ModAPI.IMySensorBlock sensorBlock)
        {
            if (sensorBlock == null)
                return;

            if (!sensorBlock.IsWorking)
                return;

            //var definition = MyDefinitionManager.Static.GetCubeBlockDefinition(sensorBlock.BlockDefinition);
            //var sensorDefinition = definition as MySensorBlockDefinition;

            sensorBlock.SetValue("Left", 6f);
            sensorBlock.SetValue("Right", 6f);
            sensorBlock.SetValue("Top", 6f);
            sensorBlock.SetValue("Bottom", 6f);
            sensorBlock.SetValue("Front", 1f);
            sensorBlock.SetValue("Back", 1f);

            if (sensorBlock.DetectPlayers)
                sensorBlock.ApplyAction("Detect Players");
            if (sensorBlock.DetectFloatingObjects)
                sensorBlock.ApplyAction("Detect Floating Objects");
            if (sensorBlock.DetectSmallShips)
                sensorBlock.ApplyAction("Detect Small Ships");
            if (sensorBlock.DetectLargeShips)
                sensorBlock.ApplyAction("Detect Large Ships");
            if (sensorBlock.DetectStations)
                sensorBlock.ApplyAction("Detect Stations");
            if (sensorBlock.DetectAsteroids)
                sensorBlock.ApplyAction("Detect Asteroids");
            if (sensorBlock.DetectOwner)
                sensorBlock.ApplyAction("Detect Owner");
            if (sensorBlock.DetectFriendly)
                sensorBlock.ApplyAction("Detect Friendly");
            if (sensorBlock.DetectNeutral)
                sensorBlock.ApplyAction("Detect Neutral");
            if (sensorBlock.DetectEnemy)
                sensorBlock.ApplyAction("Detect Enemy");
        }

        private void ResetRotor(Sandbox.ModAPI.IMyMotorStator motorBase)
        {
            if (motorBase == null)
                return;

            var definition = MyDefinitionManager.Static.GetCubeBlockDefinition(motorBase.BlockDefinition);
            var motorStatorDefinition = definition as MyMotorStatorDefinition;

            var action = motorBase.GetActionWithName("OnOff_Off");
            action.Apply(motorBase);

            motorBase.SetValue("Torque", motorStatorDefinition.MaxForceMagnitude);
            motorBase.SetValue("BrakingTorque", motorStatorDefinition.MaxForceMagnitude);
            motorBase.SetValue("Velocity", 0.0f);
        }

        private void SetupCustomTrackingProperties()
        {
            _exceptionInfo = "";

            var solarPanelEntity = (IMySensorBlock)Entity;
            if (string.IsNullOrEmpty(solarPanelEntity.CustomName))
            {
                _autoTrackOn = false;
                return;
            }

            var match = Regex.Match(solarPanelEntity.CustomName, @"(\s+|^)/AT\s*(?:\[(?<NAME>[^\]]*)\]){1,2}(\s+|$)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                match = Regex.Match(solarPanelEntity.CustomName, @"(\s+|^)/AT\s*(?:\((?<NAME>[^\)]*)\)){1,2}(\s+|$)", RegexOptions.IgnoreCase);
                if (!match.Success)
                {
                    _autoTrackOn = false;
                    return;
                }
            }

            var rotor1Name = match.Groups["NAME"].Captures[0].Value;
            var rotor2Name = match.Groups["NAME"].Captures.Count > 1 ? match.Groups["NAME"].Captures[1].Value : string.Empty;

            WriteDebug("Name Matched", "'{0}' '{1}'", rotor1Name, rotor2Name);

            _attachedRotors.Clear();
            _rotorDirections.Clear();
            IMyMotorStator motorBase1 = null;
            IMyCubeBlock motorRotor1 = null;
            IMyMotorStator motorBase2 = null;
            IMyCubeBlock motorRotor2 = null;
            int baseRotation1 = 1;
            int baseRotation2 = 1;

            var gridGroup = Entity.Parent.GetAttachedGrids();

            List<IMySlimBlock> blocks;

            foreach (var grid in gridGroup)
            {
                if (motorBase1 == null)
                {
                    blocks = new List<IMySlimBlock>();
                    grid.GetBlocks(blocks, b => b.FatBlock != null && b.FatBlock is IMyMotorStator && ((IMyTerminalBlock)b.FatBlock).CustomName.Equals(rotor1Name, StringComparison.InvariantCultureIgnoreCase));
                    if (blocks.Count > 0)
                    {
                        motorBase1 = (IMyMotorStator)blocks[0].FatBlock;
                    }
                }

                if (motorBase2 == null && !string.IsNullOrEmpty(rotor2Name))
                {
                    blocks = new List<IMySlimBlock>();
                    grid.GetBlocks(blocks, b => b.FatBlock != null && b.FatBlock is IMyMotorStator && ((IMyTerminalBlock)b.FatBlock).CustomName.Equals(rotor2Name, StringComparison.InvariantCultureIgnoreCase));

                    if (blocks.Count > 0)
                    {
                        motorBase2 = (IMyMotorStator)blocks[0].FatBlock;
                    }
                }
            }

            if (motorBase1 == motorBase2)
            {
                motorBase2 = null;
            }

            if (motorBase1 != null)
            {
                var motorBase = ((IMyCubeBlock)motorBase1).GetObjectBuilderCubeBlock() as MyObjectBuilder_MotorBase;

                foreach (var grid in gridGroup)
                {
                    blocks = new List<IMySlimBlock>();
                    grid.GetBlocks(blocks, b => b != null && b.FatBlock != null && b.FatBlock.EntityId == motorBase.RotorEntityId);

                    if (blocks.Count > 0)
                    {
                        motorRotor1 = blocks[0].FatBlock;
                        break;
                    }
                }
            }

            if (motorBase2 != null)
            {
                var motorBase = ((IMyCubeBlock)motorBase2).GetObjectBuilderCubeBlock() as MyObjectBuilder_MotorBase;

                foreach (var grid in gridGroup)
                {
                    blocks = new List<IMySlimBlock>();
                    grid.GetBlocks(blocks, b => b != null && b.FatBlock != null && b.FatBlock.EntityId == motorBase.RotorEntityId);

                    if (blocks.Count > 0)
                    {
                        motorRotor2 = blocks[0].FatBlock;
                        break;
                    }
                }
            }

            if (_attachedRotors.Count == 0)
            {
                if (FindRotorLinks((IMyCubeGrid)Entity.Parent, motorBase1, motorRotor1, ref _attachedRotors, ref baseRotation1))
                {
                    FindRotorLinks((IMyCubeGrid)_attachedRotors[_attachedRotors.Count - 1].Parent, motorBase2, motorRotor2, ref _attachedRotors, ref baseRotation2);
                }
            }
            if (_attachedRotors.Count == 0)
            {
                if (FindRotorLinks((IMyCubeGrid)Entity.Parent, motorBase2, motorRotor2, ref _attachedRotors, ref baseRotation2))
                {
                    FindRotorLinks((IMyCubeGrid)_attachedRotors[_attachedRotors.Count - 1].Parent, motorBase1, motorRotor1, ref _attachedRotors, ref baseRotation1);
                }
            }


            //_exceptionInfo += string.Format("Count: {0}. ",_attachedRotors.Count);

            if (_attachedRotors.Count >= 2)
            {
                //var cross = GetRotationAngle(solarPanelEntity.WorldMatrix, _attachedRotors[0].WorldMatrix.Up); // Unsure.
                var cross = GetRotationAngle(solarPanelEntity.LocalMatrix, _attachedRotors[0].LocalMatrix.Up); // Works
                cross.Normalize();
                //cross = Vector2.Normalize(cross);
                RotateDirections rotate = RotateDirections.Unknown;

                if (cross.Y == +1 * baseRotation1) rotate = RotateDirections.YawPositive;
                if (cross.Y == -1 * baseRotation1) rotate = RotateDirections.YawNegative;
                if (cross.X == +1 * baseRotation1) rotate = RotateDirections.PitchPositive;
                if (cross.X == -1 * baseRotation1) rotate = RotateDirections.PitchNegative;

                _rotorDirections.Add(rotate);
                // TODO: find rotation.

                //_exceptionInfo = string.Format("{0} / {1} {2} / {3}",
                //solarPanelEntity.LocalMatrix.Forward,
                //_attachedRotors[0].LocalMatrix.Up, rotate, cross);
            }

            if (_attachedRotors.Count >= 4)
            {
                var cross = GetRotationAngle(solarPanelEntity.WorldMatrix, _attachedRotors[2].WorldMatrix.Up);
                //var cross = GetRotationAngle(_attachedRotors[1].LocalMatrix, _attachedRotors[2].LocalMatrix.Up); // DOES NOT WORK.
                cross.Normalize();
                //cross = Vector2.Normalize(cross);
                RotateDirections rotate = RotateDirections.Unknown;

                if (cross.Y == -1) rotate = RotateDirections.YawPositive; //  Y
                if (cross.Y == +1) rotate = RotateDirections.YawNegative; // ??
                if (cross.X == -1) rotate = RotateDirections.PitchNegative; // N
                if (cross.X == +1) rotate = RotateDirections.PitchPositive;  // N

                _rotorDirections.Add(rotate);

                _exceptionInfo = string.Format("{0} / {1} {2} / {3}",
                _attachedRotors[1].LocalMatrix.Forward,
                _attachedRotors[2].LocalMatrix.Up, rotate, cross);
            }


            ResetRotor(motorBase1);
            ResetRotor(motorBase2);

            _autoTrackOn = true;

            WriteDebug("Attached", "{0}", _attachedRotors.Count);
        }

        private bool FindRotorLinks(IMyCubeGrid cubeGrid, IMyMotorStator motorBase, IMyCubeBlock motorRotor, ref List<IMyEntity> attachedRotors, ref int baseRotation)
        {
            if (motorBase != null)
            {
                List<IMySlimBlock> blocks = new List<IMySlimBlock>();
                cubeGrid.GetBlocks(blocks, b => b.FatBlock != null);

                if (blocks.Any(b => b.FatBlock.EntityId == motorBase.EntityId))
                {
                    attachedRotors.Add(motorBase);
                    attachedRotors.Add(motorRotor);
                    baseRotation = -1;
                    return true;
                }

                if (blocks.Any(b => b.FatBlock.EntityId == motorRotor.EntityId))
                {
                    attachedRotors.Add(motorRotor);
                    attachedRotors.Add(motorBase);
                    baseRotation = +1;
                    return true;
                }
            }

            return false;
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

        private void WriteDebug(string sender, string text, params object[] args)
        {
            if (Debug)
            {
                //if (SunSensorScript.Instance != null)
                //    SunSensorScript.Instance.ServerLogger.Write(text, args);

                //        string message = text;
                //        if (args != null && args.Length != 0)
                //            message = string.Format(text, args);

                //        MyAPIGateway.Utilities.ShowMessage(sender, message);

                //        if (MyAPIGateway.Utilities.IsDedicated)
                //            VRage.Utils.MyLog.Default.WriteLineAndConsole("##" + sender + "## " + message);
                //        else
                //            VRage.Utils.MyLog.Default.WriteLine("##" + sender + "## " + message);
            }
        }
    }
}