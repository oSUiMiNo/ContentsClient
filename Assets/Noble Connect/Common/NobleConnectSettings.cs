namespace NobleConnect
{
    using System;
    using System.Text;
    using NobleConnect.Ice;
    using UnityEngine;

    /// <summary>Settings used by Noble Connect to authenticate with the relay and punchthrough services</summary>
    public class NobleConnectSettings : ScriptableObject
    {
        /// <summary>Used to identify your game and authenticate with the relay servers</summary>
        /// <remarks>
        /// This is populated for you when you go through the setup wizard but you can also set it manually here.
        /// Your game ID is available any time on the dashboard at noblewhale.com
        /// </remarks>
        [Tooltip("Used to identify your game and authenticate with the relay servers")]
        public string gameID;

        public ushort relayServerPort = 3478;

        public static IceConfig InitConfig()
        {
            var settings = (NobleConnectSettings)Resources.Load("NobleConnectSettings", typeof(NobleConnectSettings));
            var platform = Application.platform;

            var nobleConfig = new IceConfig {
                icePort = settings.relayServerPort,
                useSimpleAddressGathering = (platform == RuntimePlatform.IPhonePlayer || platform == RuntimePlatform.Android) && !Application.isEditor
            };

            if (!string.IsNullOrEmpty(settings.gameID))
            {
                if (settings.gameID.Length % 4 != 0) throw new ArgumentException("Game ID is wrong. Re-copy it from the Dashboard on the website.");
                string decodedGameID = Encoding.UTF8.GetString(Convert.FromBase64String(settings.gameID));
                string[] parts = decodedGameID.Split('\n');

                if (parts.Length == 3)
                {
                    nobleConfig.origin = parts[0];
                    nobleConfig.username = parts[1];
                    nobleConfig.password = parts[2];
                }
            }

            nobleConfig.iceServerAddress = "auto.connect.noblewhale.com";

            return nobleConfig;
        }
    }
}
