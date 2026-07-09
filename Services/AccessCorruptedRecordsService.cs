using RRStoSQL.Models;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.IO;
using System.Linq;

namespace RRStoSQL.Services
{
    /// <summary>
    /// Выполняет поиск поврежденных записей
    /// в базе данных Microsoft Access.
    /// </summary>
    public class AccessCorruptedRecordsService
    {
        /// <summary>
        /// Путь к файлу журнала.
        /// </summary>
        private string _logFile;

        /// <summary>
        /// Признак наличия поврежденных записей.
        /// </summary>
        private bool _hasCorruptedRecords;

        /// <summary>
        /// Уже записанные в лог комбинации
        /// "Таблица -> Поле".
        /// Используется для исключения дублей.
        /// </summary>
        private readonly HashSet<string> _loggedFields =
            new HashSet<string>();

        /// <summary>
        /// Выполняет поиск поврежденных записей.
        /// </summary>
        /// <param name="mdbPath">
        /// Полный путь к MDB-файлу.
        /// </param>
        /// <returns>
        /// Список найденных поврежденных полей.
        /// </returns>
        public List<CorruptedFieldInfo> FindCorruptedRecords(
            string mdbPath)
        {
            var result =
                new List<CorruptedFieldInfo>();

            InitializeLog();

            string connectionString =
                $"Provider=Microsoft.Jet.OLEDB.4.0;" +
                $"Data Source={mdbPath};";

            using (var connection =
                new OleDbConnection(
                    connectionString))
            {
                connection.Open();

                List<TableCheckInfo> tables =
                    GetTablesForCheck();

                foreach (TableCheckInfo table in tables)
                {
                    FindTableCorruptedRecords(
                        connection,
                        table,
                        result);
                }
            }

            if (_hasCorruptedRecords)
            {
                Log("");
                Log("Проверка завершена.");
            }
            else
            {
                if (File.Exists(_logFile))
                {
                    File.Delete(_logFile);
                }
            }

            return result;
        }

        /// <summary>
        /// Создает журнал проверки.
        /// </summary>
        private void InitializeLog()
        {
            string logsFolder =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "logs");

            Directory.CreateDirectory(
                logsFolder);

            _logFile =
                Path.Combine(
                    logsFolder,
                     $"CorruptedTablesLog_{DateTime.Now:yyyyMMdd_HHmmss}.log");

            _hasCorruptedRecords = false;

            _loggedFields.Clear();

            if (File.Exists(_logFile))
            {
                File.Delete(_logFile);
            }

            Log("Проверка базы данных");
            Log("====================");
            Log("");
        }

        /// <summary>
        /// Проверяет таблицу на наличие
        /// поврежденных записей.
        /// Проверяются первые и последние
        /// пять записей таблицы.
        /// </summary>
        private void FindTableCorruptedRecords(
            OleDbConnection connection,
            TableCheckInfo table,
            List<CorruptedFieldInfo> result)
        {
            // Проверяем первые 5 записей.
            CheckRecords(
                connection,
                table,
                result,
                ascending: true);

            // Проверяем последние 5 записей.
            CheckRecords(
                connection,
                table,
                result,
                ascending: false);
        }

        /// <summary>
        /// Проверяет указанную выборку записей.
        /// </summary>
        private void CheckRecords(
            OleDbConnection connection,
            TableCheckInfo table,
            List<CorruptedFieldInfo> result,
            bool ascending)
        {
            string direction =
                ascending
                    ? "ASC"
                    : "DESC";

            string sql =
                $"SELECT TOP 5 * " +
                $"FROM [{table.TableName}] " +
                $"ORDER BY [{table.OrderField}] {direction}";

            using (var command =
                new OleDbCommand(
                    sql,
                    connection))
            {
                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        for (int i = 0;
                             i < reader.FieldCount;
                             i++)
                        {
                            object value =
                                reader.GetValue(i);

                            if (value == DBNull.Value)
                            {
                                continue;
                            }

                            string text =
                                value.ToString();

                            if (!text.Contains("####"))
                            {
                                continue;
                            }

                            string fieldName =
                                reader.GetName(i);

                            result.Add(
                                new CorruptedFieldInfo
                                {
                                    TableName =
                                        table.TableName,

                                    FieldName =
                                        fieldName,

                                    Value =
                                        text
                                });

                            _hasCorruptedRecords = true;

                            string key =
                                $"{table.TableName}|{fieldName}";

                            if (_loggedFields.Add(key))
                            {
                                Log(
                                    $"{table.TableName} -> {fieldName}");
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Проверяет одну таблицу
        /// на наличие поврежденных записей.
        /// </summary>
        //private void FindTableCorruptedRecords(
        //    OleDbConnection connection,
        //    TableCheckInfo table,
        //    List<CorruptedFieldInfo> result)
        //{
        //    string sql =
        //        $"SELECT TOP 5 * " +
        //        $"FROM [{table.TableName}] " +
        //        $"ORDER BY [{table.OrderField}]";

        //    using (var command =
        //        new OleDbCommand(
        //            sql,
        //            connection))
        //    {
        //        using (var reader =
        //            command.ExecuteReader())
        //        {
        //            while (reader.Read())
        //            {
        //                for (int i = 0;
        //                     i < reader.FieldCount;
        //                     i++)
        //                {
        //                    object value =
        //                        reader.GetValue(i);

        //                    if (value == DBNull.Value)
        //                    {
        //                        continue;
        //                    }

        //                    string text =
        //                        value.ToString();

        //                    if (!text.Contains("#####"))
        //                    {
        //                        continue;
        //                    }

        //                    string fieldName =
        //                        reader.GetName(i);

        //                    result.Add(
        //                        new CorruptedFieldInfo
        //                        {
        //                            TableName =
        //                                table.TableName,

        //                            FieldName =
        //                                fieldName,

        //                            Value =
        //                                text
        //                        });

        //                    _hasCorruptedRecords = true;

        //                    string key =
        //                        $"{table.TableName}|{fieldName}";

        //                    if (_loggedFields.Add(key))
        //                    {
        //                        Log(
        //                            $"{table.TableName} -> {fieldName}");
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}
        /// <summary>
        /// Возвращает список таблиц,
        /// которые необходимо проверить.
        /// </summary>
        private List<TableCheckInfo> GetTablesForCheck()
        {
            var tables =
                new List<TableCheckInfo>
                {
                    //new TableCheckInfo
                    //{
                    //    TableName = "OrderTarget",
                    //    OrderField = "OrderId"
                    //},

                    new TableCheckInfo
                    {
                        TableName = "Order",
                        OrderField = "Id"
                    }
                };

            string cfg =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    @"Misc\CorruptedTables.cfg");

            if (!File.Exists(cfg))
            {
                return tables;
            }

            foreach (string line in File.ReadAllLines(cfg))
            {
                string text =
                    line.Trim();

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (text.StartsWith("#"))
                {
                    continue;
                }

                string[] parts =
                    text.Split(',');

                if (parts.Length != 2)
                {
                    continue;
                }

                tables.Add(
                    new TableCheckInfo
                    {
                        TableName =
                            parts[0].Trim(),

                        OrderField =
                            parts[1].Trim()
                    });
            }

            return tables;
        }

        /// <summary>
        /// Записывает строку в журнал.
        /// </summary>
        private void Log(
            string text)
        {
            File.AppendAllText(
                _logFile,
                text + Environment.NewLine);
        }
    }
}



//using RRStoSQL.Models;
//using System;
//using System.Collections.Generic;
//using System.Data.OleDb;
//using System.IO;

//namespace RRStoSQL.Services
//{
//    /// <summary>
//    /// Выполняет поиск поврежденных записей
//    /// в базе данных Microsoft Access.
//    /// </summary>
//    public class AccessCorruptedRecordsService
//    {

//        /// <summary>
//        /// Путь к файлу лога.
//        /// </summary>
//        private string _logFile;

//        /// <summary>
//        /// Признак наличия поврежденных записей.
//        /// </summary>
//        private bool _hasCorruptedRecords;

//        /// <summary>
//        /// Возвращает список поврежденных полей.
//        /// </summary>
//        /// <param name="mdbPath">
//        /// Полный путь к MDB-файлу.
//        /// </param>
//        public List<CorruptedFieldInfo> FindCorruptedRecords(
//            string mdbPath)
//        {
//            var result =
//                new List<CorruptedFieldInfo>();

//            string connectionString =
//                $"Provider=Microsoft.Jet.OLEDB.4.0;" +
//                $"Data Source={mdbPath};";

//            using (var connection =
//                new OleDbConnection(connectionString))
//            {
//                connection.Open();

//                List<TableCheckInfo> tables =
//                    GetTablesForCheck();

//                foreach (TableCheckInfo table in tables)
//                {
//                    FindTableCorruptedRecords(
//                        connection,
//                        table,
//                        result);
//                }
//            }

//            return result;
//        }

//        /// <summary>
//        /// Проверяет одну таблицу.
//        /// </summary>
//        private void FindTableCorruptedRecords(
//            OleDbConnection connection,
//            TableCheckInfo table,
//            List<CorruptedFieldInfo> result)
//        {
//            string logsFolder =
//                Path.Combine(
//                    AppDomain.CurrentDomain.BaseDirectory,
//                    "logs");

//            Directory.CreateDirectory(
//                logsFolder);

//            _logFile =
//                Path.Combine(
//                    logsFolder,
//                    "CorruptedTablesLog.log");

//            _hasCorruptedRecords = false;

//            if (File.Exists(_logFile))
//            {
//                File.Delete(_logFile);
//            }

//            Log(
//                $"Проверка базы данных:");

//            Log("");

//            string sql =
//                $"SELECT TOP 5 * FROM [{table.TableName}] " +
//                $"ORDER BY [{table.OrderField}]";

//            using (var command =
//                new OleDbCommand(
//                    sql,
//                    connection))
//            {
//                using (var reader =
//                    command.ExecuteReader())
//                {
//                    while (reader.Read())
//                    {
//                        for (int i = 0;
//                             i < reader.FieldCount;
//                             i++)
//                        {
//                            object value =
//                                reader.GetValue(i);

//                            if (value == DBNull.Value)
//                            {
//                                continue;
//                            }

//                            string text =
//                                value.ToString();

//                            if (text.Contains("#####"))
//                            {
//                                string fieldName =
//                                    reader.GetName(i);

//                                result.Add(
//                                    new CorruptedFieldInfo
//                                    {
//                                        TableName = table.TableName,
//                                        FieldName = fieldName,
//                                        Value = text
//                                    });

//                                _hasCorruptedRecords = true;

//                                Log(
//                                    $"{table.TableName} -> {fieldName}");
//                                //result.Add(
//                                //    new CorruptedFieldInfo
//                                //    {
//                                //        TableName =
//                                //            table.TableName,

//                                //        FieldName =
//                                //            reader.GetName(i),

//                                //        Value =
//                                //            text
//                                //    });
//                                //_hasCorruptedRecords = true;

//                                //Log(
//                                //    $"{table.TableName} -> {columnName}");


//                            }
//                        }
//                    }
//                }
//            }

//            if (_hasCorruptedRecords)
//            {
//                Log("");
//                Log("Проверка завершена.");
//            }
//            else
//            {
//                if (File.Exists(_logFile))
//                {
//                    File.Delete(_logFile);
//                }
//            }

//        }

//        /// <summary>
//        /// Возвращает список таблиц,
//        /// подлежащих проверке.
//        /// </summary>
//        private List<TableCheckInfo> GetTablesForCheck()
//        {
//            var tables =
//                new List<TableCheckInfo>();

//            tables.Add(
//                new TableCheckInfo
//                {
//                    TableName = "OrderTarget",
//                    OrderField = "OrderId"
//                });

//            tables.Add(
//                new TableCheckInfo
//                {
//                    TableName = "Order",
//                    OrderField = "Id"
//                });

//            string cfg =
//                Path.Combine(
//                    AppDomain.CurrentDomain.BaseDirectory,
//                    @"Misc\CorruptedTables.cfg");

//            if (!File.Exists(cfg))
//            {
//                return tables;
//            }

//            string[] lines =
//                File.ReadAllLines(cfg);

//            foreach (string line in lines)
//            {
//                string text =
//                    line.Trim();

//                if (text.Length == 0)
//                {
//                    continue;
//                }

//                if (text.StartsWith("#"))
//                {
//                    continue;
//                }

//                string[] parts =
//                    text.Split(',');

//                if (parts.Length != 2)
//                {
//                    continue;
//                }

//                tables.Add(
//                    new TableCheckInfo
//                    {
//                        TableName =
//                            parts[0].Trim(),

//                        OrderField =
//                            parts[1].Trim()
//                    });
//            }

//            return tables;
//        }

//        /// <summary>
//        /// Записывает строку в лог.
//        /// </summary>
//        private void Log(
//            string text)
//        {
//            File.AppendAllText(
//                _logFile,
//                text + Environment.NewLine);
//        }
//    }
//}