namespace midspace.SolarTracker
{
    using System;
    using Sandbox.ModAPI;
    using VRage.Game.ObjectBuilders;
    using VRageMath;

    public static class Support
    {
        private static double _calcFrequency;
        private static DateTime _lastFetch;
        private static Vector3D _sunDirection;
        private static Vector3 _baseSunDirection;
        private static Vector3 _sunRotationAxis;
        private static bool _hasBaseSun;

        /// <summary>
        /// Logic is duplicated from VRage.Game.MySunProperties.
        /// </summary>
        /// <returns></returns>
        public static Vector3D GetSunDirection()
        {
            // 820 milliseconds lead time in calculation, to offset for fast sun movement.
            const double leadTime = 0.820 / 60;

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

                if (!_hasBaseSun)
                {
                    GetBaseSunDirection(out _baseSunDirection, out _sunRotationAxis);
                    _hasBaseSun = true;
                }

                if (MyAPIGateway.Session.SessionSettings.EnableSunRotation)
                {
                    float angle = MathHelper.TwoPi * (float)((MyAPIGateway.Session.ElapsedGameTime().TotalMinutes + leadTime) / MyAPIGateway.Session.SessionSettings.SunRotationIntervalMinutes);
                    Vector3 finalSunDirection = Vector3.Transform(_baseSunDirection, Matrix.CreateFromAxisAngle(_sunRotationAxis, angle));
                    finalSunDirection.Normalize();
                    _sunDirection = finalSunDirection;
                }
                else
                    _sunDirection = _baseSunDirection;
            }
            return _sunDirection;
        }

        private static void GetBaseSunDirection(out Vector3 baseSunDirection, out Vector3 sunRotationAxis)
        {
            baseSunDirection = Vector3.Zero;

            // -- MySector.InitEnvironmentSettings --
            var environment = MyAPIGateway.Session.GetSector().Environment;
            Vector3 baseSunDirectionNormalized;
            Vector3.CreateFromAzimuthAndElevation(environment.SunAzimuth, environment.SunElevation, out baseSunDirectionNormalized);

            if (!MyAPIGateway.Session.SessionSettings.EnableSunRotation)
            {
                baseSunDirection = baseSunDirectionNormalized;
                sunRotationAxis = Vector3.Zero;
                return;
            }

            // -- Sandbox.Game.SessionComponents.MySectorWeatherComponent.Init() --
            var cpnt = MyAPIGateway.Session.GetCheckpoint("null");
            MyObjectBuilder_SectorWeatherComponent weatherComp = null;
            foreach (var comp in cpnt.SessionComponents)
            {
                MyObjectBuilder_SectorWeatherComponent component = comp as MyObjectBuilder_SectorWeatherComponent;
                if (component != null)
                    weatherComp = component;
            }

            if (weatherComp != null && !weatherComp.BaseSunDirection.IsZero)
                baseSunDirection = weatherComp.BaseSunDirection;

            // -- Sandbox.Game.SessionComponents.MySectorWeatherComponent.BeforeStart() -- 
            float num = Math.Abs(baseSunDirection.X) + Math.Abs(baseSunDirection.Y) + Math.Abs(baseSunDirection.Z);
            if ((double)num < 0.001)
                baseSunDirection = baseSunDirectionNormalized;

            // -- VRage.Game.MySunProperties.SunRotationAxis --
            float num2 = Math.Abs(Vector3.Dot(baseSunDirectionNormalized, Vector3.Up));
            Vector3 result;
            if (num2 > 0.95f)
                result = Vector3.Cross(Vector3.Cross(baseSunDirectionNormalized, Vector3.Left), baseSunDirectionNormalized);
            else
                result = Vector3.Cross(Vector3.Cross(baseSunDirectionNormalized, Vector3.Up), baseSunDirectionNormalized);
            result.Normalize();
            sunRotationAxis = result;
        }
    }
}