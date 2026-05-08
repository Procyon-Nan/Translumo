using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Translumo.Infrastructure.Constants
{
    public static class Global
    {
        public static string AppPath;

        static Global()
        {
#if DEBUG
            AppPath = System.AppDomain.CurrentDomain.BaseDirectory;
#else
            AppPath = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
#endif
        }


        public static Version GetVersion()
        {
            return new Version(FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location).ProductVersion);
        }
    }
}
