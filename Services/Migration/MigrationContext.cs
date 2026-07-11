using System.Windows.Forms;
using RRStoSQL.Models;

namespace RRStoSQL.Services.Migration
{
    /// <summary>
    /// Общий контекст миграции.
    /// </summary>
    public class MigrationContext
    {
        public Form Owner { get; set; }

        public AppConfig Config { get; set; }

        public string ConfigPath { get; set; }

        public bool IsNewDatabase { get; set; }
    }
}