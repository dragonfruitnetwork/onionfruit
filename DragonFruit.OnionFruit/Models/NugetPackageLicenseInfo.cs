// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

namespace DragonFruit.OnionFruit.Models
{
    public record NugetPackageLicenseInfo(
        string PackageId,
        string PackageVersion,
        string PackageProjectUrl,
        string Copyright,
        string Authors,
        string License,
        string LicenseUrl
    )
    {
        public bool HasLicense => !string.IsNullOrEmpty(License) || !string.IsNullOrEmpty(LicenseUrl);
        public bool HasProjectUrl => !string.IsNullOrEmpty(PackageProjectUrl);

        public void OpenProjectWebsite() => App.Launch(PackageProjectUrl);
    }
}