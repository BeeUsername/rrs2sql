namespace RRStoSQL.Models
{
    public class MigrationOptions
    {
        /// <summary>
        /// Установить SQL Server Management Studio
        /// после завершения миграции.
        /// </summary>
        public bool InstallSsms
        {
            get;
            set;
        }

        /// <summary>
        /// Удалить Bullzip после успешной конвертации.
        /// </summary>
        public bool DeleteBullzip
        {
            get;
            set;
        }
    }
}