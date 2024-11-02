// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Serialization;
using DragonFruit.Data;
using DragonFruit.Data.Serializers;
using DragonFruit.OnionFruit.Models;

namespace DragonFruit.OnionFruit.Services
{
    public class OnionFruitClient : ApiClient<ApiJsonSerializer>
    {
        public OnionFruitClient()
        {
            UserAgent = $"OnionFruit/v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(IEnumerable<NugetPackageLicenseInfo>))]
    public partial class OnionFruitSerializerContext : JsonSerializerContext;
}