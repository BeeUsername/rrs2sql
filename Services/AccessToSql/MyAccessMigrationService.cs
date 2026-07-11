using System;
using System.Data.SqlClient;
using RRStoSQL.Models;

namespace RRStoSQL.Services.AccessToSql
{
    /// <summary>
    /// Выполняет полную миграцию базы данных
    /// Access в SQL Server.
    /// </summary>
    public class MyAccessMigrationService
    {
        /// <summary>
        /// Читает структуру MDB.
        /// </summary>
        private readonly AccessDatabaseReader
            _databaseReader =
                new AccessDatabaseReader();

        /// <summary>
        /// Создает таблицы SQL Server.
        /// </summary>
        private readonly SqlTableCreator
            _tableCreator =
                new SqlTableCreator();
        private readonly SqlBulkCopyService
            _bulkCopy =
                new SqlBulkCopyService();
        /// <summary>
        /// Выполняет миграцию.
        /// </summary>
        public void Migrate(
      AppConfig config)
        {
            AccessDatabaseInfo database =
                _databaseReader.Read(
                    config.MdbPath);

            using (SqlConnection connection =
                new SqlConnection(
                    BuildConnectionString(config)))
            {
                connection.Open();

                _tableCreator.CreateTables(
                    connection,
                    database);

                //_bulkCopy.CopyTables(...);
            }
        }

        /// <summary>
        /// Формирует строку подключения
        /// к SQL Server.
        /// </summary>
        private string BuildConnectionString(
            AppConfig config)
        {
            return
                $"Server={config.SqlInstance};" +
                $"Database={config.SqlDatabaseName};" +
                "User Id=RRUser;" +
                "Password=RRS_999999999!;" +
                "TrustServerCertificate=True;";
        }


    }
}