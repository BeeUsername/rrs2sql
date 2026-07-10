using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Data.SqlClient;
using RRStoSQL.Services.AccessToSql.Models;

namespace RRStoSQL.Services.AccessToSql
{
    /// <summary>
    /// Выполняет перенос данных
    /// из Access в SQL Server
    /// посредством SqlBulkCopy.
    /// </summary>
    public class SqlBulkCopyService
    {
        /// <summary>
        /// Событие вызывается
        /// после копирования очередной партии строк.
        /// </summary>
        public event Action<string, long> RowsCopied;

        /// <summary>
        /// Копирует список таблиц.
        /// </summary>
        public void CopyTables(
            OleDbConnection accessConnection,
            SqlConnection sqlConnection,
            List<AccessTableInfo> tables)
        {
            foreach (AccessTableInfo table in tables)
            {
                CopyTable(
                    accessConnection,
                    sqlConnection,
                    table.Name);
            }
        }

        /// <summary>
        /// Копирует одну таблицу.
        /// </summary>
        private void CopyTable(
            OleDbConnection accessConnection,
            SqlConnection sqlConnection,
            string tableName)
        {
            using (var command =
                new OleDbCommand(
                    $"SELECT * FROM [{tableName}]",
                    accessConnection))
            {
                using (var reader =
                    command.ExecuteReader())
                {
                    using (var bulk =
                        CreateBulkCopy(
                            sqlConnection,
                            tableName))
                    {
                        bulk.SqlRowsCopied +=
                            (sender, e) =>
                            {
                                RowsCopied?.Invoke(
                                    tableName,
                                    e.RowsCopied);
                            };

                        for (int i = 0;
                             i < reader.FieldCount;
                             i++)
                        {
                            string column =
                                reader.GetName(i);

                            bulk.ColumnMappings.Add(
                                column,
                                column);
                        }

                        bulk.WriteToServer(reader);
                    }
                }
            }
        }

        /// <summary>
        /// Создает SqlBulkCopy.
        /// </summary>
        private SqlBulkCopy CreateBulkCopy(
            SqlConnection connection,
            string tableName)
        {
            SqlBulkCopy bulk =
                new SqlBulkCopy(connection);

            bulk.DestinationTableName =
                $"[{tableName}]";

            bulk.BatchSize = 5000;

            bulk.NotifyAfter = 5000;

            bulk.BulkCopyTimeout = 0;

            return bulk;
        }
    }
}