namespace midspace.SolarTracker
{
    using System;
    using Sandbox.ModAPI;
    using VRage.Game.ObjectBuilders;
    using VRageMath;
#if !STABLE
    using Sandbox.Definitions;
    using VRage.Game;
#endif

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

                float angle = 0;
                if (MyAPIGateway.Session.SessionSettings.EnableSunRotation)
                    angle = MathHelper.TwoPi * (float)((MyAPIGateway.Session.ElapsedGameTime().TotalMinutes + leadTime) / MyAPIGateway.Session.SessionSettings.SunRotationIntervalMinutes);

                Vector3 finalSunDirection = Vector3.Transform(_baseSunDirection, Matrix.CreateFromAxisAngle(_sunRotationAxis, angle));
                finalSunDirection.Normalize();
#if STABLE
                _sunDirection = -finalSunDirection;
#endif
#if !STABLE
                _sunDirection = finalSunDirection;
#endif
            }
            return _sunDirection;
        }

        private static void GetBaseSunDirection(out Vector3 baseSunDirection, out Vector3 sunRotationAxis)
        {
            baseSunDirection = Vector3.Zero;

            // -- Sandbox.Game.SessionComponents.MySectorWeatherComponent.Init() --
            var cpnt = MyAPIGateway.Session.GetCheckpoint("null");
            foreach (var comp in cpnt.SessionComponents)
            {
                var weatherComp = comp as MyObjectBuilder_SectorWeatherComponent;
                if (weatherComp != null)
                {
                    baseSunDirection = weatherComp.BaseSunDirection;
                }
            }

#if STABLE
            float num = Math.Abs(Vector3.Dot(baseSunDirection, Vector3.Up));
            if (num > 0.95f)
                sunRotationAxis = Vector3.Cross(Vector3.Cross(baseSunDirection, Vector3.Left), baseSunDirection);
            else
                sunRotationAxis = Vector3.Cross(Vector3.Cross(baseSunDirection, Vector3.Up), baseSunDirection);
            sunRotationAxis.Normalize();
#endif

#if !STABLE
            var ed = ((MyObjectBuilder_EnvironmentDefinition)MyDefinitionManager.Static.EnvironmentDefinition.GetObjectBuilder());
            if (Vector3.IsZero(baseSunDirection))
                baseSunDirection = ed.SunProperties.SunDirectionNormalized;

            // -- Sandbox.Game.SessionComponents.MySectorWeatherComponent.BeforeStart() --
            float num = Math.Abs(baseSunDirection.X) + Math.Abs(baseSunDirection.Y) + Math.Abs(baseSunDirection.Z);
            if ((double)num < 0.001)
                baseSunDirection = ed.SunProperties.BaseSunDirectionNormalized;

            // -- VRage.Game.MySunProperties.SunRotationAxis --
            num = Math.Abs(Vector3.Dot(baseSunDirection, Vector3.Up));
            Vector3 result;
            if (num > 0.95f)
                result = Vector3.Cross(Vector3.Cross(baseSunDirection, Vector3.Left), baseSunDirection);
            else
                result = Vector3.Cross(Vector3.Cross(baseSunDirection, Vector3.Up), baseSunDirection);
            result.Normalize();
            sunRotationAxis = result;
#endif
        }
    }
}