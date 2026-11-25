using System;
using System.Linq;

namespace NatureCalls.Core
{
    /// <summary>How this process runs. A dedicated server is a Linux server build (UNITY_SERVER) or any build started with -server.</summary>
    public static class RuntimeMode
    {
        public static bool Dedicated
        {
            get
            {
#if UNITY_SERVER
                return true;
#else
                return Environment.GetCommandLineArgs().Contains("-server");
#endif
            }
        }
    }
}
