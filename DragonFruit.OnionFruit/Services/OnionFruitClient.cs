// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Reflection;
using DragonFruit.Data;
using DragonFruit.Data.Serializers;

namespace DragonFruit.OnionFruit.Services
{
    public class OnionFruitClient : ApiClient<ApiJsonSerializer>
    {
        public OnionFruitClient()
        {
            UserAgent = $"OnionFruit/v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
        }
    }
}