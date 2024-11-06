// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using DragonFruit.OnionFruit.Core.Config;
using DragonFruit.OnionFruit.Core.Transports;
using DragonFruit.OnionFruit.Updater;
using DynamicData;
using Microsoft.Extensions.Logging;

namespace DragonFruit.OnionFruit.Configuration
{
    public partial class OnionFruitSettingsStore
    {
        /// <summary>
        /// Reads legacy settings from the config.json file, applying them to the current settings file.
        /// </summary>
        private void MigrateLegacySettings()
        {
            var legacySettingsPath = Path.Combine(App.StoragePath, "config.json");

            if (!File.Exists(legacySettingsPath))
            {
                return;
            }

            JsonObject legacySettings;

            using (var fileStream = File.OpenRead(legacySettingsPath))
            {
                legacySettings = (JsonObject)JsonNode.Parse(fileStream);
            }

            try
            {
                // countries
                SetValue(OnionFruitSetting.TorEntryCountryCode, legacySettings!["tor_entry"]!["a2c"]?.GetValue<string>());
                SetValue(OnionFruitSetting.TorExitCountryCode, legacySettings["tor_exit"]!["a2c"]?.GetValue<string>());

                // features
                SetValue(OnionFruitSetting.EnableErrorReporting, legacySettings["functions"]!["crash_reports"]?.GetValue<bool>() != false);
                SetValue(OnionFruitSetting.EnableDiscordRpc, legacySettings["functions"]["discord_status"]?.GetValue<bool>() == true);
                SetValue(OnionFruitSetting.ExplicitUpdateStream, legacySettings["functions"]["beta_updates"]?.GetValue<bool>() == true ? (UpdateStream?)UpdateStream.Beta : null);
                SetValue(OnionFruitSetting.DisconnectOnTorFailure, legacySettings["functions"]["killswitch"]?.GetValue<bool>() == false);

                // landing pages
                if (legacySettings.ContainsKey("connected_landing"))
                {
                    SetValue(OnionFruitSetting.EnableWebsiteLaunchConnect, legacySettings["connected_landing"]!["enabled"]?.GetValue<bool>() ?? true);
                    SetValue(OnionFruitSetting.WebsiteLaunchConnect, legacySettings["connected_landing"]["location"]?.GetValue<string>());
                }

                if (legacySettings.ContainsKey("disconnected_landing"))
                {
                    SetValue(OnionFruitSetting.EnableWebsiteLaunchDisconnect, legacySettings["disconnected_landing"]!["enabled"]?.GetValue<bool>() ?? false);
                    SetValue(OnionFruitSetting.WebsiteLaunchDisconnect, legacySettings["disconnected_landing"]["location"]?.GetValue<string>());
                }

                // bridges
                if (legacySettings["bridges"]?["enabled"]?.GetValue<bool>() == true)
                {
                    SetValue(OnionFruitSetting.SelectedTransportType, legacySettings["bridges"]["bridge_type"]!.GetValue<int>() switch
                    {
                        0 => TransportType.Plain,
                        1 => TransportType.meek,
                        3 => TransportType.obfs3,
                        4 => TransportType.obfs4,
                        5 => TransportType.scramblesuit,
                        6 => TransportType.snowflake,

                        _ => TransportType.None
                    });
                }

                // bridge lines
                if (legacySettings.ContainsKey("bridges"))
                {
                    var lines = legacySettings["bridges"]!["bridge_lines"]?.GetValue<string>().Split('\n').Where(x => BridgeEntry.ValidationRegex().IsMatch(x)) ?? [];
                    GetCollection<string>(OnionFruitSetting.UserDefinedBridges).AddRange(lines);
                }

#if !DEBUG
                File.Delete(legacySettingsPath);
#endif
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to migrate OnionFruit 5 settings file: {message}", e.Message);
            }
        }
    }
}