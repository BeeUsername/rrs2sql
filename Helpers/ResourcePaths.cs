using System;
using System.IO;

namespace RRStoSQL.Helpers
{
    internal static class ResourcePaths
    {
        public static string Root =>
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Resources");

        public static string Bullzip =>
            Path.Combine(Root, "bullzip");

        public static string SqlDistrib =>
            Path.Combine(Root, "sql_distrib");

        public static string SqlPatches =>
            Path.Combine(Root, "sql_patches");

        public static string AccessPatches =>
            Path.Combine(Root, "access_patches");

        public static string WebInstallers =>
            Path.Combine(Root, "web_installers");

        public static string Misc =>
            Path.Combine(Root, "misc");
    }
}