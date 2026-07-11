using System;
using System.Data;
using System.Data.OleDb;
using RRStoSQL.Services.AccessToSql.Models;

namespace RRStoSQL.Services.AccessToSql
{
    /// <summary>
    /// Читает полную структуру Access базы данных.
    /// На выходе формируется объектная модель,
    /// которая используется всеми сервисами миграции.
    /// </summary>
    public class AccessDatabaseReader
    {
        /// <summary>
        /// Читает структуру базы данных.
        /// </summary>
        public AccessDatabaseInfo Read(string mdbFile)
        {
            var database = new AccessDatabaseInfo
            {
                FileName = mdbFile
            };

            using (var connection =
                new OleDbConnection(
                    BuildConnectionString(mdbFile)))
            {
                connection.Open();

                ReadTables(connection, database);
                ReadAllColumns(connection, database);

                ReadPrimaryKeys(connection, database);

                ReadIndexes(connection, database);
                ReadForeignKeys(connection, database);
            }

            return database;
        }

        /// <summary>
        /// Читает список пользовательских таблиц.
        /// </summary>
        private void ReadTables(
            OleDbConnection connection,
            AccessDatabaseInfo database)
        {
            DataTable schema =
                connection.GetSchema("Tables");

            foreach (DataRow row in schema.Rows)
            {
                string type =
                    row["TABLE_TYPE"].ToString();

                if (!type.Equals(
                    "TABLE",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string tableName =
                    row["TABLE_NAME"].ToString();

                if (IsSystemTable(tableName))
                {
                    continue;
                }

                var table =
                    new AccessTableInfo
                    {
                        Name = tableName
                    };

                database.Tables.Add(table);

                database.TableMap.Add(
                    tableName,
                    table);
            }
        }

        /// <summary>
        /// Читает описание всех колонок базы данных.
        /// </summary>
        private void ReadAllColumns(
            OleDbConnection connection,
            AccessDatabaseInfo database)
        {
            DataTable schema =
                connection.GetSchema("Columns");

            foreach (DataRow row in schema.Rows)
            {
                string tableName =
                    row["TABLE_NAME"].ToString();

                if (!database.TableMap.TryGetValue(
                        tableName,
                        out AccessTableInfo table))
                {
                    continue;
                }

                AccessColumnInfo column =
                    ReadColumn(row);

                table.Columns.Add(column);

                table.ColumnMap.Add(
                    column.Name,
                    column);
            }
        }

        /// <summary>
        /// Читает описание одной колонки.
        /// </summary>
        private AccessColumnInfo ReadColumn(
            DataRow row)
        {
            var column =
                new AccessColumnInfo();

            column.Name =
                row["COLUMN_NAME"].ToString();

            column.AccessType =
                (OleDbType)Convert.ToInt32(
                    row["DATA_TYPE"]);

            if (row["CHARACTER_MAXIMUM_LENGTH"] != DBNull.Value)
            {
                column.Length =
                    Convert.ToInt32(
                        row["CHARACTER_MAXIMUM_LENGTH"]);
            }

            if (row["IS_NULLABLE"] != DBNull.Value)
            {
                column.AllowNull =
                    row["IS_NULLABLE"]
                        .ToString()
                        .Equals(
                            "YES",
                            StringComparison.OrdinalIgnoreCase);
            }

            // AutoNumber определим позже.
            column.IsIdentity = false;

            return column;
        }

        /// <summary>
        /// Читает первичные ключи всех таблиц.
        /// </summary>
        private void ReadPrimaryKeys(
            OleDbConnection connection,
            AccessDatabaseInfo database)
        {
            DataTable schema =
                connection.GetOleDbSchemaTable(
                    OleDbSchemaGuid.Primary_Keys,
                    null);

            if (schema == null)
            {
                return;
            }

            foreach (DataRow row in schema.Rows)
            {
                string tableName =
                    row["TABLE_NAME"].ToString();

                if (!database.TableMap.TryGetValue(
                        tableName,
                        out AccessTableInfo table))
                {
                    continue;
                }

                string columnName =
                    row["COLUMN_NAME"].ToString();

                if (!table.PrimaryKeyColumns.Contains(
                        columnName))
                {
                    table.PrimaryKeyColumns.Add(
                        columnName);
                }
            }
        }

        /// <summary>
        /// Читает индексы.
        /// </summary>
        private void ReadIndexes(
            OleDbConnection connection,
            AccessDatabaseInfo database)
        {
            // Реализуем следующим этапом.
        }

        /// <summary>
        /// Читает внешние ключи.
        /// </summary>
        private void ReadForeignKeys(
            OleDbConnection connection,
            AccessDatabaseInfo database)
        {
            // Реализуем следующим этапом.
        }

        /// <summary>
        /// Формирует строку подключения к Access.
        /// </summary>
        private string BuildConnectionString(
            string mdbFile)
        {
            return
                @"Provider=Microsoft.ACE.OLEDB.12.0;" +
                $"Data Source={mdbFile};" +
                "Persist Security Info=False;";
        }

        /// <summary>
        /// Проверяет, является ли таблица системной.
        /// </summary>
        private bool IsSystemTable(
            string tableName)
        {
            return tableName.StartsWith(
                "MSys",
                StringComparison.OrdinalIgnoreCase);
        }
    }
}