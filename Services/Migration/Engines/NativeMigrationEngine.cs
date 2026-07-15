using RRStoSQL.Services.Migration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.IO;
using System.Linq;

namespace RRStoSQL.Services.Migration.Engines
{
    /// <summary>
    /// Выполняет нативную миграцию Access → SQL.
    /// </summary>
    public class NativeMigrationEngine
    {
        private string _mdbPath;
        private string _sqlConnStr;
        private string _logFilePath;

        private string MdbConnStr => $@"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={_mdbPath};";

        private class ColumnInfo
        {
            public string Name;
            public Type NetType;
            public string SqlType;
            public bool IsAutoIncrement;
            public bool AllowDBNull;
        }

        public void Run(
            MigrationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(
                    nameof(context));
            }

            if (context.Config == null)
            {
                throw new Exception(
                    "Не загружена конфигурация миграции.");
            }

            // ============================================
            // Получаем параметры миграции
            // ============================================

            var appConfig =
                context.Config;

            //var owner =
            //    context.Owner;

            string mdbPath =
                appConfig.MdbPath;

            string sqlInstance =
                appConfig.SqlInstance;

            string databaseName =
                appConfig.SqlDatabaseName;

            // ============================================
            // Инициализация: пути, строка подключения, лог
            // ============================================

            _mdbPath = mdbPath;
            _sqlConnStr = $@"Server={sqlInstance};Database={databaseName};Trusted_Connection=True;";

            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            _logFilePath = Path.Combine(logDir, $"ConvertationLog_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");

            EnsureDatabaseExists(
                sqlInstance,
                databaseName);


            //MigrateAll();
            //ExecuteMigration();

            try
            {
                Log("=== Начало миграции ===");

                ExecuteMigration();

                Log("=== Миграция успешно завершена ===");
            }
            catch (Exception ex)
            {
                Log("");
                Log("=== ОШИБКА ===");
                Log(ex.ToString());

                throw;
            }
            finally
            {
                Log("=== Завершение работы ===");
            }
        }

        // ---------- Логирование ----------

        void Log(string message)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            try
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Не удалось записать лог: {ex.Message}");
            }
        }

        // ---------- Главный метод миграции ----------

        void ExecuteMigration()
        {
            Log($"=== Начало миграции: {_mdbPath} -> {_sqlConnStr} ===");

            var tables = GetTableNames();
            var tableColumns = new Dictionary<string, List<ColumnInfo>>();

            // 1. Схема + создание таблиц (без FK)
            foreach (var table in tables)
            {
                Log($"Создание таблицы: {table}");
                var cols = GetColumns(table);
                tableColumns[table] = cols;
                CreateSqlTable(table, cols);
            }

            // 2. Данные (с сохранением identity)
            foreach (var table in tables)
            {
                Log($"Копирование данных: {table}");
                CopyData(table, tableColumns[table]);
            }

            // 3. Индексы (кроме PK, он уже создан вместе с таблицей)
            foreach (var table in tables)
            {
                Log($"Индексы: {table}");
                CreateIndexes(table);
            }

            // 4. Внешние ключи — в самом конце
            Log("Внешние ключи...");
            CreateForeignKeys();

            Log("=== Миграция завершена ===");
        }

        // ---------- Таблицы ----------

        List<string> GetTableNames()
        {
            var tables = new List<string>();
            using (var conn = new OleDbConnection(MdbConnStr))
            {
                conn.Open();

                var schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables,
                    new object[] { null, null, null, "TABLE" });

                foreach (DataRow row in schema.Rows)
                {
                    var name = row["TABLE_NAME"].ToString();
                    if (!name.StartsWith("MSys"))
                        tables.Add(name);
                }
            }
            return tables;
        }

        // ВАЖНО: SELECT без "WHERE 1=0" + CommandBehavior.KeyInfo — иначе Jet OLEDB
        // ненадёжно возвращает IsAutoIncrement/AllowDBNull (часть колонок может
        // определиться неверно, что ломает IDENTITY и NOT NULL в целевой БД).
        List<ColumnInfo> GetColumns(string tableName)
        {
            var result = new List<ColumnInfo>();
            using (var mdbConn = new OleDbConnection(MdbConnStr))
            {
                mdbConn.Open();

                using (var cmd = new OleDbCommand($"SELECT * FROM [{tableName}]", mdbConn))
                using (var reader = cmd.ExecuteReader(CommandBehavior.KeyInfo))
                {
                    var schemaTable = reader.GetSchemaTable();
                    foreach (DataRow col in schemaTable.Rows)
                    {
                        var type = (Type)col["DataType"];
                        bool isAutoInc = col["IsAutoIncrement"] != DBNull.Value && (bool)col["IsAutoIncrement"];
                        bool allowNull = col["AllowDBNull"] == DBNull.Value || (bool)col["AllowDBNull"];

                        result.Add(new ColumnInfo
                        {
                            Name = col["ColumnName"].ToString(),
                            NetType = type,
                            SqlType = MapType(type, col),
                            IsAutoIncrement = isAutoInc,
                            AllowDBNull = allowNull
                        });
                    }
                }
            }
            return result;
        }

        void CreateSqlTable(string tableName, List<ColumnInfo> columns)
        {
            var pkColumns = GetPrimaryKeyColumns(tableName);
            var defs = new List<string>();

            foreach (var c in columns)
            {
                string nullability = pkColumns.Contains(c.Name) ? "NOT NULL"
                                      : (c.AllowDBNull ? "NULL" : "NOT NULL");
                string identity = c.IsAutoIncrement ? "IDENTITY(1,1)" : "";
                defs.Add($"[{c.Name}] {c.SqlType} {identity} {nullability}".Trim());
            }

            string pkConstraint = "";
            if (pkColumns.Count > 0)
            {
                string pkCols = string.Join(", ", pkColumns.Select(c => $"[{c}]"));
                pkConstraint = $", CONSTRAINT [PK_{tableName}] PRIMARY KEY ({pkCols})";
            }

            string createSql =
                $"IF OBJECT_ID('{tableName}', 'U') IS NOT NULL DROP TABLE [{tableName}]; " +
                $"CREATE TABLE [{tableName}] ({string.Join(", ", defs)}{pkConstraint})";

            using (var sqlConn = new SqlConnection(_sqlConnStr))
            {
                sqlConn.Open();
                using (var sqlCmd = new SqlCommand(createSql, sqlConn))
                {
                    sqlCmd.ExecuteNonQuery();
                }
            }
        }

        List<string> GetPrimaryKeyColumns(string tableName)
        {
            var result = new List<string>();
            using (var conn = new OleDbConnection(MdbConnStr))
            {
                conn.Open();

                var schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Primary_Keys,
                    new object[] { null, null, tableName });

                foreach (DataRow row in schema.Rows)
                    result.Add(row["COLUMN_NAME"].ToString());
            }
            return result;
        }

        string MapType(Type netType, DataRow colSchema)
        {
            if (netType == typeof(string))
            {
                return $"NVARCHAR({GetSize(colSchema)})";
            }
            else if (netType == typeof(int))
            {
                return "INT";
            }
            else if (netType == typeof(short))
            {
                return "SMALLINT";
            }
            else if (netType == typeof(long))
            {
                return "BIGINT";
            }
            else if (netType == typeof(byte))
            {
                return "TINYINT";
            }
            else if (netType == typeof(double))
            {
                return "FLOAT";
            }
            else if (netType == typeof(float))
            {
                return "REAL";
            }
            else if (netType == typeof(decimal))
            {
                return "DECIMAL(18,4)";
            }
            else if (netType == typeof(DateTime))
            {
                return "DATETIME2"; // диапазон 0001-9999, а не 1753-9999 как у DATETIME
            }
            else if (netType == typeof(bool))
            {
                return "BIT";
            }
            else if (netType == typeof(Guid))
            {
                return "UNIQUEIDENTIFIER";
            }
            else if (netType == typeof(byte[]))
            {
                return "VARBINARY(MAX)";
            }
            else
            {
                return "NVARCHAR(MAX)";
            }
        }

        string GetSize(DataRow colSchema)
        {
            var size = colSchema["ColumnSize"];
            if (size == null || size == DBNull.Value) return "MAX";
            int s = Convert.ToInt32(size);
            return s <= 0 || s > 4000 ? "MAX" : s.ToString();
        }

        // ---------- Данные ----------

        void CopyData(string tableName, List<ColumnInfo> columns)
        {
            using (var mdbConn = new OleDbConnection(MdbConnStr))
            {
                mdbConn.Open();

                using (var cmd = new OleDbCommand($"SELECT * FROM [{tableName}]", mdbConn))
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.HasRows) return;

                    bool hasIdentity = columns.Any(c => c.IsAutoIncrement);

                    var options = SqlBulkCopyOptions.Default;
                    if (hasIdentity)
                        options |= SqlBulkCopyOptions.KeepIdentity; // сохраняем исходные значения счётчика

                    using (var sqlConn = new SqlConnection(_sqlConnStr))
                    {
                        sqlConn.Open();
                        using (var bulkCopy = new SqlBulkCopy(sqlConn, options, null))
                        {
                            bulkCopy.DestinationTableName = $"[{tableName}]"; // экранирование обязательно
                                                                              // (например, таблица "Order")
                            bulkCopy.BulkCopyTimeout = 0;

                            foreach (var c in columns)
                                bulkCopy.ColumnMappings.Add(c.Name, c.Name);

                            bulkCopy.WriteToServer(reader);
                        }
                    }
                }
            }
        }

        // ---------- Индексы ----------

        void CreateIndexes(string tableName)
        {
            using (var conn = new OleDbConnection(MdbConnStr))
            {
                conn.Open();

                var schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Indexes,
                    new object[] { null, null, null, null, tableName });

                var grouped = schema.Rows.Cast<DataRow>()
                    .Where(r => Convert.ToBoolean(r["PRIMARY_KEY"]) == false) // PK уже создан вместе с таблицей
                    .GroupBy(r => r["INDEX_NAME"].ToString());

                using (var sqlConn = new SqlConnection(_sqlConnStr))
                {
                    sqlConn.Open();

                    foreach (var group in grouped)
                    {
                        var ordered = group.OrderBy(r => Convert.ToInt16(r["ORDINAL_POSITION"])).ToList();
                        bool isUnique = Convert.ToBoolean(ordered[0]["UNIQUE"]);
                        string indexName = $"IX_{tableName}_{group.Key}".Replace(" ", "_");
                        string cols = string.Join(", ", ordered.Select(r =>
                        {
                            string colName = r["COLUMN_NAME"].ToString();
                            short collation = Convert.ToInt16(r["COLLATION"]);
                            string dir = collation == 2 ? "DESC" : "ASC";
                            return $"[{colName}] {dir}";
                        }));

                        string uniqueKw = isUnique ? "UNIQUE" : "";
                        string sql = $"CREATE {uniqueKw} INDEX [{indexName}] ON [{tableName}] ({cols})";

                        try
                        {
                            using (var sqlCmd = new SqlCommand(sql, sqlConn))
                            {
                                sqlCmd.ExecuteNonQuery();
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"  Пропущен индекс {indexName}: {ex.Message}");
                        }
                    }
                }
            }
        }

        // ---------- Внешние ключи ----------

        void CreateForeignKeys()
        {
            DataTable schema;
            using (var conn = new OleDbConnection(MdbConnStr))
            {
                conn.Open();

                try
                {
                    schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Foreign_Keys,
                        new object[] { null, null, null, null, null, null });
                }
                catch
                {
                    Log("  В базе не заданы связи (Relationships) или они не читаются провайдером.");
                    return;
                }
            }

            var grouped = schema.Rows.Cast<DataRow>().GroupBy(r => r["FK_NAME"].ToString());

            using (var sqlConn = new SqlConnection(_sqlConnStr))
            {
                sqlConn.Open();

                foreach (var group in grouped)
                {
                    var rows = group.OrderBy(r => Convert.ToInt16(r["ORDINAL"])).ToList();
                    var first = rows[0];

                    string fkTable = first["FK_TABLE_NAME"].ToString();
                    string pkTable = first["PK_TABLE_NAME"].ToString();
                    string fkCols = string.Join(", ", rows.Select(r => $"[{r["FK_COLUMN_NAME"]}]"));
                    string pkCols = string.Join(", ", rows.Select(r => $"[{r["PK_COLUMN_NAME"]}]"));
                    string fkName = $"FK_{fkTable}_{pkTable}_{group.Key}".Replace(" ", "_");

                    string sql = $@"ALTER TABLE [{fkTable}] ADD CONSTRAINT [{fkName}]
                                 FOREIGN KEY ({fkCols}) REFERENCES [{pkTable}] ({pkCols})";

                    try
                    {
                        using (var sqlCmd = new SqlCommand(sql, sqlConn))
                        {
                            sqlCmd.ExecuteNonQuery();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"  Пропущен FK {fkName}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Проверяет существование базы данных.
        /// Если базы нет — создаёт её.
        /// </summary>
        private void EnsureDatabaseExists(
            string sqlInstance,
            string databaseName)
        {
            var builder =
                new SqlConnectionStringBuilder
                {
                    DataSource = sqlInstance,
                    InitialCatalog = "master",
                    IntegratedSecurity = true,
                    TrustServerCertificate = true
                };

            using (var connection =
                new SqlConnection(
                    builder.ConnectionString))
            {
                connection.Open();

                using (var command =
                    connection.CreateCommand())
                {
                    command.CommandText =
                        @"
IF DB_ID(@DatabaseName) IS NULL
BEGIN
    EXEC('CREATE DATABASE [' + @DatabaseName + ']')
END";

                    command.Parameters.AddWithValue(
                        "@DatabaseName",
                        databaseName);

                    command.ExecuteNonQuery();
                }
            }

            Log(
                $"База данных [{databaseName}] готова.");
        }
    }
}