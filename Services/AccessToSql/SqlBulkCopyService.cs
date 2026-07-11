using System;
using System.Data.OleDb;
using System.Data.SqlClient;
using RRStoSQL.Services.AccessToSql.Models;

namespace RRStoSQL.Services.AccessToSql
{
    /// <summary>
    /// Переносит данные из Access
    /// в SQL Server.
    /// </summary>
    public class SqlBulkCopyService
    {
        /// <summary>
        /// Копирует все таблицы.
        /// </summary>
        public void CopyTables(
            string accessConnectionString,
            SqlConnection sqlConnection,
            AccessDatabaseInfo database)
        {
            using (OleDbConnection accessConnection =
                new OleDbConnection(
                    accessConnectionString))
            {
                accessConnection.Open();

                foreach (AccessTableInfo table in database.Tables)
                {
                    CopyTable(
                        accessConnection,
                        sqlConnection,
                        table);
                }
            }
        }

        /// <summary>
        /// Копирует одну таблицу.
        /// </summary>
        private void CopyTable(
            OleDbConnection accessConnection,
            SqlConnection sqlConnection,
            AccessTableInfo table)
        {
            string sql =
                $"SELECT * FROM [{table.Name}]";

            using (OleDbCommand command =
                new OleDbCommand(
                    sql,
                    accessConnection))
            {
                using (OleDbDataReader reader =
                    command.ExecuteReader())
                {
                    using (SqlBulkCopy bulk =
                        new SqlBulkCopy(sqlConnection))
                    {
                        bulk.DestinationTableName =
                            $"[{table.Name}]";

                        bulk.BatchSize = 5000;

                        bulk.BulkCopyTimeout = 0;

                        //----------------------------------
                        // сопоставляем поля
                        //----------------------------------

                        foreach (AccessColumnInfo column in table.Columns)
                        {
                            bulk.ColumnMappings.Add(
                                column.Name,
                                column.Name);
                        }

                        bulk.WriteToServer(reader);
                    }
                }
            }
        }
    }
}