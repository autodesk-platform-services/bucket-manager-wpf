using Autodesk.SDKManager;

namespace bucket.manager.wpf.APSUtils
{
    // SDK Manager Helper, singleton instance.
    internal class SdkManagerHelper 
    {
        private static SDKManager? _instance = null;

        public static SDKManager Instance
        {
            get { return _instance ??= SdkManagerBuilder.Create().Build(); }
        }

    }
}
