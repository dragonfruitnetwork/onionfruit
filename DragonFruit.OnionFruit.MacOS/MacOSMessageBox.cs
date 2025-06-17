// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

namespace DragonFruit.OnionFruit.MacOS
{
    public static class MacOSMessageBox
    {
        /// <summary>
        /// Shows a macOS-style alert dialog with the specified title, message, and button text.
        /// </summary>
        public static void Show(string title, string message, string buttonText = "OK")
        {
            NativeMethods.ShowMessageBox(title, message, buttonText);
        }
    }
}