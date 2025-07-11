// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using DragonFruit.OnionFruit.Services;

namespace DragonFruit.OnionFruit.Windows
{
    public class WindowsApiClient : OnionFruitClient
    {
        public WindowsApiClient()
        {
            UserAgent = $"OnionFruitWin/v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
            Handler = () => new WinHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                AutomaticRedirection = true,
                CookieUsePolicy = CookieUsePolicy.IgnoreCookies,
                SendTimeout = TimeSpan.FromSeconds(10),
                ReceiveHeadersTimeout = TimeSpan.FromSeconds(15),
                WindowsProxyUsePolicy = WindowsProxyUsePolicy.UseWinInetProxy
            };
        }
    }
}