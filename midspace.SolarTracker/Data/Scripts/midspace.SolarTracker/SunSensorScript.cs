namespace midspace.SolarTracker
{
    using System;
    using System.Timers;
    using Sandbox.Common;
    using Sandbox.Common.ObjectBuilders;
    using Sandbox.ModAPI;
    using VRage.Game;
    using VRage.Game.Components;

    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class SunSensorScript : MySessionComponentBase
    {
        #region fields

        private bool _isInitialized;
        //private bool _isClientRegistered;
        public bool IsServerRegistered;
        private Timer _timerEvents;

        public static SunSensorScript Instance;

        public TextLogger ServerLogger = new TextLogger();
        //public TextLogger ClientLogger = new TextLogger();

        #endregion

        #region attaching events and wiring up

        public override void UpdateAfterSimulation()
        {
            if (Instance == null)
                Instance = this;

            // This needs to wait until the MyAPIGateway.Session.Player is created, as running on a Dedicated server can cause issues.
            // It would be nicer to just read a property that indicates this is a dedicated server, and simply return.
            if (!_isInitialized && MyAPIGateway.Session != null && MyAPIGateway.Session.Player != null)
            {
                if (MyAPIGateway.Session.OnlineMode.Equals(MyOnlineModeEnum.OFFLINE)) // pretend single player instance is also server.
                    InitServer();
                if (!MyAPIGateway.Session.OnlineMode.Equals(MyOnlineModeEnum.OFFLINE) && MyAPIGateway.Multiplayer.IsServer && !MyAPIGateway.Utilities.IsDedicated)
                    InitServer();
                //InitClient();
            }

            // Dedicated Server.
            if (!_isInitialized && MyAPIGateway.Utilities != null && MyAPIGateway.Multiplayer != null
                && MyAPIGateway.Session != null && MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Multiplayer.IsServer)
            {
                InitServer();
                return;
            }

            base.UpdateAfterSimulation();
        }

        //private void InitClient()
        //{
        //    _isInitialized = true; // Set this first to block any other calls from UpdateAfterSimulation().
        //    _isClientRegistered = true;

        //    ClientLogger.Init("SunSensorClient.Log"); // comment this out if logging is not required for the Client.
        //    ClientLogger.Write("SunSensor Client Log Started");
        //    if (ClientLogger.IsActive)
        //        VRage.Utils.MyLog.Default.WriteLine(String.Format("##Mod## SunSensor Client Logging File: {0}", ClientLogger.LogFile));
        //}

        private void InitServer()
        {
            _isInitialized = true; // Set this first to block any other calls from UpdateAfterSimulation().
            IsServerRegistered = true;
            ServerLogger.Init("SunSensorServer.Log", false, 0); // comment this out if logging is not required for the Server.
            ServerLogger.Write("SunSensor Server Log Started");
            if (ServerLogger.IsActive)
                VRage.Utils.MyLog.Default.WriteLine(String.Format("##Mod## SunSensor Server Logging File: {0}", ServerLogger.LogFile));

            // start the timer last, as all data should be loaded before this point.
            ServerLogger.Write("Attaching Event timer.");
            _timerEvents = new Timer(5000);
            _timerEvents.Elapsed += TimerEventsOnElapsed;
            _timerEvents.Start();
        }

        #endregion

        #region detaching events

        protected override void UnloadData()
        {
            //if (_isClientRegistered)
            //{
            //    ClientLogger.Write("Closed");
            //    ClientLogger.Terminate();
            //}

            if (IsServerRegistered)
            {
                if (_timerEvents != null)
                {
                    ServerLogger.Write("Stopping Event timer.");
                    _timerEvents.Stop();
                    _timerEvents.Elapsed -= TimerEventsOnElapsed;
                    _timerEvents = null;
                }

                ServerLogger.Write("Closed");
            }

            ServerLogger.Terminate();
            base.UnloadData();
        }

        #endregion

        #region message handling

        private void TimerEventsOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            // DO NOT SET ANY IN GAME API CALLS HERE. AT ALL!
            MyAPIGateway.Utilities.InvokeOnGameThread(delegate ()
            {
                // Recheck main Gateway properties, as the Game world my be currently shutting down when the InvokeOnGameThread is called.
                if (MyAPIGateway.Players == null || MyAPIGateway.Entities == null || MyAPIGateway.Session == null || MyAPIGateway.Utilities == null)
                    return;

                // Any processing needs to occur in here, as it will be on the main thread, and hopefully thread safe.
                // : here
            });
        }

        #endregion
    }
}