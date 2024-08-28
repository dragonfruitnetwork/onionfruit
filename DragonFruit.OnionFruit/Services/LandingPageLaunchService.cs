// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Threading;
using System.Threading.Tasks;
using DragonFruit.OnionFruit.Configuration;
using DragonFruit.OnionFruit.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DragonFruit.OnionFruit.Services
{
    /// <summary>
    /// Service responsible for launching landing pages when session states change
    /// </summary>
    public class LandingPageLaunchService(TorSession session, OnionFruitSettingsStore settings, ILogger<LandingPageLaunchService> logger) : IHostedService
    {
        public static readonly string DefaultConnectionPage = "https://dragonfruit.network/onionfruit/status";

        private void OnSessionStateChanged(object sender, TorSession.TorSessionState e)
        {
            bool? shouldLaunchPage = e switch
            {
                TorSession.TorSessionState.Connected => settings.GetValue<bool>(OnionFruitSetting.EnableWebsiteLaunchConnect),
                TorSession.TorSessionState.Disconnected => settings.GetValue<bool>(OnionFruitSetting.EnableWebsiteLaunchDisconnect),

                _ => null
            };

            switch (shouldLaunchPage)
            {
                case null:
                    return;

                case false:
                    logger.LogDebug("Landing page launch disabled for {state}", e);
                    return;

                default:
                    var url = settings.GetValue<string>(e == TorSession.TorSessionState.Connected ? OnionFruitSetting.WebsiteLaunchConnect : OnionFruitSetting.WebsiteLaunchDisconnect);
                    if (string.IsNullOrEmpty(url))
                    {
                        url = DefaultConnectionPage;
                    }

                    logger.LogInformation("Launching landing page {url} for {state}", url, e);
                    App.Launch(url);
                    break;
            }
        }

        Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            session.SessionStateChanged += OnSessionStateChanged;
            return Task.CompletedTask;
        }

        Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            session.SessionStateChanged -= OnSessionStateChanged;
            return Task.CompletedTask;
        }
    }
}