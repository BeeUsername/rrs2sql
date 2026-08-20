using RRStoSQL.Helpers;
using RRStoSQL.Models;
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
    /// Основной движок миграции Access -> SQL Server.
    ///
    /// Алгоритм:
    ///
    /// 1. Создание SQL базы из шаблона.
    /// 2. Переключение конфигурации УР на SQL.
    /// 3. Запуск УР.
    /// 4. Ожидание окончания обновления структуры.
    /// 5. Закрытие УР.
    /// 6. Чтение таблиц Access.
    /// 7. Для каждой таблицы (в несколько проходов, см. CopyData):
    ///      Access -> временная SQL таблица
    ///      временная таблица -> рабочая таблица
    ///      вставляются только отсутствующие PK.
    ///
    /// Важный принцип:
    /// данные из Access не должны безусловно вставляться
    /// непосредственно в рабочую SQL таблицу.
    ///
    /// ИСПРАВЛЕНИЯ (2026-08-10):
    ///
    /// 1. IDENTITY_INSERT.
    ///    Для таблиц со столбцом IDENTITY вставка явных значений PK
    ///    требует SET IDENTITY_INSERT ON/OFF. Раньше это не делалось,
    ///    из-за чего часть таблиц падала с ошибкой SQL 544.
    ///
    /// 6. KeepIdentity во временной таблице (КРИТИЧНЫЙ ФИКС).
    ///    Временная таблица создаётся через SELECT TOP 0 * INTO,
    ///    что копирует свойство IDENTITY столбца Id. При заливке
    ///    Access -> временная таблица через SqlBulkCopy БЕЗ флага
    ///    KeepIdentity, реальный Id из Access отбрасывался, и
    ///    identity-столбец временной таблицы сам присваивал новый
    ///    порядковый номер (просто по порядку строк). Дальше этот
    ///    уже испорченный Id вставлялся в рабочую таблицу как "настоящий".
    ///    Итог: Id в SQL переставал совпадать с Access (SQL шёл
    ///    плотной последовательностью без дыр, даже если в Access
    ///    были дыры от удалённых строк) — и все дочерние таблицы,
    ///    ссылающиеся на исходный Access.Id (например OrderTarget),
    ///    оказывались "сиротами" без единой настоящей ошибки FK.
    ///
    /// 2. Порядок таблиц / FOREIGN KEY.
    ///    Таблицы обрабатывались в алфавитном порядке, а не в порядке
    ///    зависимостей FK, из-за чего дочерние таблицы иногда
    ///    вставлялись раньше родительских и падали по FK. Теперь
    ///    порядок таблиц вычисляется один раз заранее топологической
    ///    сортировкой по графу FK (GetTableProcessingOrder) —
    ///    родительские таблицы гарантированно идут раньше дочерних,
    ///    без многократных повторных проходов.
    ///
    /// 3. GUID -> VARCHAR.
    ///    Access может хранить значение как Guid, а в SQL столбец
    ///    имеет тип varchar/nvarchar/char. SqlBulkCopy не умеет
    ///    конвертировать Guid в строку автоматически, поэтому
    ///    поток данных из Access оборачивается в
    ///    AccessSafeDataReader, который отдаёт такие значения
    ///    уже как строки.
    ///
    /// 4. Строки-сироты (недостающий FK).
    ///    Если таблица падает по FOREIGN KEY даже при правильном
    ///    порядке — это, как правило, не порядок обработки, а
    ///    реальные "осиротевшие" строки в Access (FK ссылается на
    ///    запись, которой физически не существует). Такие строки НЕ
    ///    переносятся и НЕ пропускаются молча: LogOrphanDiagnostics
    ///    подробно логирует, какой именно FK и сколько строк виноваты
    ///    (с примерами PK), после чего CopyData бросает исключение и
    ///    останавливает миграцию целиком — до тех пор, пока проблема
    ///    не будет разобрана и исправлена в Access вручную.
    ///
    /// 5. Даты вне диапазона SQL Server.
    ///    SQL Server datetime понимает только 1/1/1753..12/31/9999,
    ///    Access может хранить и более широкий диапазон (например,
    ///    "битые" даты вроде 0001-01-01). AccessSafeDataReader
    ///    подменяет такие значения на NULL вместо падения
    ///    SqlBulkCopy, и логирует, в каком столбце и сколько раз
    ///    это произошло.
    /// </summary>
    public class NativeMigrationEngine
    {
        private readonly SqlService _sqlService =
            new SqlService();

        private MigrationContext _context;

        private AppConfig _config;

        private string _databaseName;

        private string _masterConnectionString;

        private string _sqlConnectionString;

        private string _templateFolder;

        private string _sqlDataFolder;

        private readonly object _logLock =
            new object();

        private string _logFilePath;


        // ============================================================
        // PUBLIC
        // ============================================================

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
                    "Не загружена конфигурация.");
            }

            _context =
                context;

            _config =
                context.Config;

            InitializeLog();

            Log("============================================");
            Log("Запуск NativeMigrationEngine");
            Log("============================================");

            Initialize();

            PrepareDatabase();

            CopyData();

            Log("============================================");
            Log("Миграция завершена.");
            Log("============================================");
        }


        // ============================================================
        // INITIALIZE
        // ============================================================

        private void Initialize()
        {
            _databaseName =
                _config.SqlDatabaseName;

            _masterConnectionString =
                $"Server={_config.SqlInstance};" +
                $"Database=master;" +
                $"Trusted_Connection=True;";

            _sqlConnectionString =
                $"Server={_config.SqlInstance};" +
                $"Database={_databaseName};" +
                $"Trusted_Connection=True;";

            _templateFolder =
                Path.Combine(
                    ResourcePaths.Misc,
                    "mssqlBlankBase");

            _sqlDataFolder =
                _sqlService.GetDefaultDataFolder(
                    _config.SqlInstance);

            Log($"SQL Instance: {_config.SqlInstance}");
            Log($"SQL Database: {_databaseName}");
            Log($"Access database: {_config.MdbPath}");
            Log($"Template folder: {_templateFolder}");
            Log($"SQL DATA folder: {_sqlDataFolder}");
        }


        // ============================================================
        // DATABASE PREPARATION
        // ============================================================

        private void PrepareDatabase()
        {
            Log("--------------------------------------------");
            Log("Подготовка SQL базы");
            Log("--------------------------------------------");

            CreateDatabaseFromTemplate();

            Log("SQL база создана.");


            //            // ========================================================
            //            // Выдаем RRUser права db_owner
            //            // ========================================================

            Log("Выдаём RRUser права db_owner...");

            _sqlService.GrantDatabaseRights(
                _config.SqlInstance,
                _databaseName);

            Log("RRUser получил права db_owner.");

            SwitchRemoteRegToSql();

            StartRemoteReg();

            WaitForDatabaseUpgrade();

            CloseRemoteReg();

            Log("Подготовка SQL базы завершена.");
        }


        private void CreateDatabaseFromTemplate()
        {
            Log("Создаем SQL базу из шаблона...");

            _sqlService.CreateDatabaseFromTemplate(
                _config.SqlInstance,
                _databaseName,
                _templateFolder);
        }


        private void SwitchRemoteRegToSql()
        {
            Log("Переключаем УР на SQL...");

            var configService =
                new ConfigService();

            configService.SwitchToSql(
                _context.ConfigPath,
                _config);

            Log("УР переключена на SQL.");
        }


        private RemoteRegController _remoteReg;

        private void StartRemoteReg()
        {
            Log("Запускаем УР...");

            _remoteReg =
                new RemoteRegController();

            _remoteReg.Start(
                _config);

            Log("УР запущена.");
        }


        private void WaitForDatabaseUpgrade()
        {
            Log("Ожидаем завершения обновления структуры...");

            _sqlService.WaitForDatabaseUpgrade(
                _config.SqlInstance,
                _databaseName);

            Log("Обновление структуры завершено.");
        }


        private void CloseRemoteReg()
        {
            if (_remoteReg == null)
            {
                return;
            }

            Log("Закрываем УР...");

            _remoteReg.CloseGracefully();

            _remoteReg = null;

            Log("УР закрыта.");
        }


        // ============================================================
        // DATA COPY
        // ============================================================

        private void CopyData()
        {
            Log("--------------------------------------------");
            Log("Начинаем перенос данных");
            Log("--------------------------------------------");

            List<string> tables =
                GetAccessTables();

            Log(
                $"Найдено таблиц Access: {tables.Count}");

            // Порядок обработки считаем один раз заранее — по
            // зависимостям FOREIGN KEY между таблицами (топологическая
            // сортировка), а не по алфавиту. Это позволяет обойтись
            // одним проходом вместо многократных повторов.

            List<string> orderedTables =
                GetTableProcessingOrder(
                    tables);

            foreach (string tableName in orderedTables)
            {
                try
                {
                    CopyTable(
                        tableName);
                }
                catch (Exception ex)
                {
                    Log(
                        $"ОШИБКА таблицы {tableName}:");

                    Log(
                        ex.ToString());

                    // Если ошибка похожа на нарушение FOREIGN KEY —
                    // строки-сироты в исходной Access базе (FK
                    // ссылается на несуществующую запись). Такие
                    // строки не пропускаются молча — диагностика
                    // (какой именно FK и сколько строк) логируется
                    // подробно, после чего миграция останавливается
                    // целиком, чтобы проблему разобрали вручную.

                    if (IsForeignKeyViolation(
                        ex))
                    {
                        try
                        {
                            LogOrphanDiagnostics(
                                tableName);
                        }
                        catch (Exception diagEx)
                        {
                            Log(
                                $"Не удалось выполнить диагностику " +
                                $"строк-сирот для {tableName}:");

                            Log(
                                diagEx.ToString());
                        }
                    }

                    Log("--------------------------------------------");

                    Log(
                        $"Миграция остановлена: таблица {tableName} " +
                        $"не перенесена (см. диагностику выше).");

                    Log("--------------------------------------------");

                    throw new Exception(
                        $"Миграция остановлена на таблице {tableName}: " +
                        $"{ex.Message}",
                        ex);
                }
            }

            Log("--------------------------------------------");
            Log("Перенос данных завершен.");
            Log("--------------------------------------------");
        }


        /// <summary>
        /// Проверяет, является ли ошибка нарушением FOREIGN KEY
        /// (SQL Server error 547). Проверка по номеру ошибки, а не по
        /// тексту сообщения — не зависит от языка локализации SQL
        /// Server.
        /// </summary>
        private bool IsForeignKeyViolation(
            Exception ex)
        {
            return
                ex is SqlException sqlEx &&
                sqlEx.Number == 547;
        }


        // ============================================================
        // TABLE PROCESSING ORDER (по зависимостям FK)
        // ============================================================

        /// <summary>
        /// Строит порядок обработки таблиц так, чтобы таблица, на
        /// которую ссылаются другие (родительская, по FK), переносилась
        /// раньше дочерних. Обычная топологическая сортировка (DFS) по
        /// графу внешних ключей, ограниченному только таблицами из
        /// списка tables. Циклические зависимости FK (если есть)
        /// не приводят к зависанию — такая таблица просто попадёт в
        /// порядок как есть, а если из-за этого возникнет FK-ошибка,
        /// её продиагностирует и остановит миграцию LogOrphanDiagnostics.
        /// </summary>
        private List<string> GetTableProcessingOrder(
            List<string> tables)
        {
            Log(
                "Определяем порядок таблиц по зависимостям FK...");

            var tableSet =
                new HashSet<string>(
                    tables,
                    StringComparer.OrdinalIgnoreCase);

            var dependencies =
                new Dictionary<string, HashSet<string>>(
                    StringComparer.OrdinalIgnoreCase);

            using (var connection =
                new SqlConnection(
                    _sqlConnectionString))
            {
                connection.Open();

                foreach (string tableName in tables)
                {
                    List<(string Column, string RefTable, string RefColumn)> fks =
                        GetForeignKeys(
                            connection,
                            tableName);

                    var deps =
                        new HashSet<string>(
                            StringComparer.OrdinalIgnoreCase);

                    foreach (var fk in fks)
                    {
                        if (tableSet.Contains(fk.RefTable) &&
                            !string.Equals(
                                fk.RefTable,
                                tableName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            deps.Add(
                                fk.RefTable);
                        }
                    }

                    dependencies[tableName] =
                        deps;
                }
            }

            var ordered =
                new List<string>();

            var visited =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            var visiting =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            void Visit(
                string tableName)
            {
                if (visited.Contains(tableName))
                {
                    return;
                }

                if (visiting.Contains(tableName))
                {
                    // Циклическая зависимость FK — оставляем как есть,
                    // дальше её вытянет спасательный проход.
                    return;
                }

                visiting.Add(
                    tableName);

                foreach (string dep in dependencies[tableName]
                    .OrderBy(
                        x => x,
                        StringComparer.OrdinalIgnoreCase))
                {
                    Visit(
                        dep);
                }

                visiting.Remove(
                    tableName);

                visited.Add(
                    tableName);

                ordered.Add(
                    tableName);
            }

            foreach (string tableName in tables
                .OrderBy(
                    x => x,
                    StringComparer.OrdinalIgnoreCase))
            {
                Visit(
                    tableName);
            }

            Log(
                "Порядок таблиц определен.");

            return ordered;
        }


        // ============================================================
        // COPY ONE TABLE
        // ============================================================

        private void CopyTable(
            string tableName)
        {
            Log("");
            Log($"Таблица: {tableName}");

            List<string> accessColumns =
                GetAccessColumns(
                    tableName);

            List<string> sqlColumns =
                GetSqlColumns(
                    tableName);

            Log(
                $"  Access columns: {accessColumns.Count}");

            Log(
                $"  SQL columns: {sqlColumns.Count}");

            List<string> commonColumns =
                accessColumns
                    .Intersect(
                        sqlColumns,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            if (commonColumns.Count == 0)
            {
                Log(
                    "  Общих полей нет. Таблица пропущена.");

                return;
            }

            List<string> missingInSql =
                accessColumns
                    .Except(
                        sqlColumns,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            foreach (string column in missingInSql)
            {
                Log(
                    $"  Поле Access отсутствует в SQL: {column}");
            }

            List<string> pkColumns;

            bool hasIdentity;

            using (var connection =
                new SqlConnection(
                    _sqlConnectionString))
            {
                connection.Open();

                pkColumns =
                    GetPrimaryKeyColumns(
                        connection,
                        tableName);

                hasIdentity =
                    HasIdentityColumn(
                        connection,
                        tableName);
            }

            if (pkColumns.Count == 0)
            {
                Log(
                    "  PRIMARY KEY отсутствует.");

                CopyTableWithoutPrimaryKey(
                    tableName,
                    commonColumns,
                    hasIdentity);

                return;
            }

            Log(
                $"  PRIMARY KEY: {string.Join(", ", pkColumns)}");

            if (hasIdentity)
            {
                Log(
                    "  Обнаружен IDENTITY столбец.");
            }

            int inserted =
                CopyTableWithPrimaryKey(
                    tableName,
                    commonColumns,
                    pkColumns,
                    hasIdentity);

            Log(
                $"  Итог: вставлено {inserted} строк.");

            Log(
                "  Готово.");
        }


        // ============================================================
        // TABLE WITHOUT PRIMARY KEY
        // ============================================================

        private void CopyTableWithoutPrimaryKey(
            string tableName,
            List<string> commonColumns,
            bool hasIdentity)
        {
            Log(
                "  Таблица без PK.");

            Log(
                "  Выполняем обычный перенос.");

            string columnList =
                BuildColumnList(
                    commonColumns);

            string accessSql =
                $"SELECT {columnList} " +
                $"FROM [{tableName}]";

            using (var accessConnection =
                new OleDbConnection(
                    GetAccessConnectionString()))
            using (var sqlConnection =
                new SqlConnection(
                    _sqlConnectionString))
            {
                accessConnection.Open();
                sqlConnection.Open();

                using (var command =
                    new OleDbCommand(
                        accessSql,
                        accessConnection))
                using (var rawReader =
                    command.ExecuteReader())
                // ФИКС №3/№5: оборачиваем reader, чтобы Guid отдавался
                // как string, а даты вне диапазона SQL Server (1753-9999)
                // подменялись на NULL вместо падения всей таблицы.
                using (var reader =
                    new AccessSafeDataReader(
                        rawReader,
                        Log))
                using (var bulk =
                    new SqlBulkCopy(
                        sqlConnection,
                        hasIdentity
                            ? SqlBulkCopyOptions.TableLock |
                              SqlBulkCopyOptions.KeepIdentity
                            : SqlBulkCopyOptions.TableLock,
                        null))
                {
                    bulk.DestinationTableName =
                        $"[dbo].[{tableName}]";

                    bulk.BatchSize =
                        1000;

                    bulk.NotifyAfter =
                        1000;

                    bulk.BulkCopyTimeout =
                        0;

                    bulk.EnableStreaming =
                        true;

                    bulk.SqlRowsCopied +=
                        (sender, args) =>
                        {
                            Log(
                                $"  {tableName}: " +
                                $"{args.RowsCopied} строк...");
                        };

                    foreach (string column in commonColumns)
                    {
                        bulk.ColumnMappings.Add(
                            column,
                            column);
                    }

                    bulk.WriteToServer(
                        reader);

                    reader.LogAnomalies(
                        tableName);
                }
            }
        }


        // ============================================================
        // TABLE WITH PRIMARY KEY
        // ============================================================

        private int CopyTableWithPrimaryKey(
            string tableName,
            List<string> commonColumns,
            List<string> pkColumns,
            bool hasIdentity)
        {
            string tempTable =
                BuildTempTableName(
                    tableName);

            using (var sqlConnection =
                new SqlConnection(
                    _sqlConnectionString))
            {
                sqlConnection.Open();

                try
                {
                    CreateTempTable(
                        sqlConnection,
                        tableName,
                        tempTable);

                    LoadAccessIntoTempTable(
                        sqlConnection,
                        tableName,
                        tempTable,
                        commonColumns,
                        hasIdentity);

                    int inserted =
                        InsertMissingRows(
                            sqlConnection,
                            tableName,
                            tempTable,
                            commonColumns,
                            pkColumns,
                            hasIdentity);

                    return inserted;
                }
                finally
                {
                    DropTempTable(
                        sqlConnection,
                        tempTable);
                }
            }
        }


        // ============================================================
        // TEMP TABLE
        // ============================================================

        private string BuildTempTableName(
            string tableName)
        {
            string safeName =
                new string(
                    tableName
                        .Where(
                            c =>
                                char.IsLetterOrDigit(c) ||
                                c == '_')
                        .ToArray());

            if (String.IsNullOrWhiteSpace(
                safeName))
            {
                safeName =
                    "Table";
            }

            return
                $"#RRStoSQL_{safeName}";
        }


        private void CreateTempTable(
            SqlConnection connection,
            string tableName,
            string tempTable)
        {
            Log(
                $"  Создаем временную таблицу {tempTable}...");

            string sql =
                $@"
SELECT TOP 0 *
INTO {tempTable}
FROM [dbo].[{tableName}];
";

            using (var command =
                new SqlCommand(
                    sql,
                    connection))
            {
                command.CommandTimeout =
                    0;

                command.ExecuteNonQuery();
            }
        }


        private void DropTempTable(
            SqlConnection connection,
            string tempTable)
        {
            try
            {
                string sql =
                    $@"
IF OBJECT_ID('tempdb..{tempTable.TrimStart('#')}')
    IS NOT NULL
    DROP TABLE {tempTable};
";

                using (var command =
                    new SqlCommand(
                        sql,
                        connection))
                {
                    command.CommandTimeout =
                        0;

                    command.ExecuteNonQuery();
                }

                Log(
                    $"  Временная таблица {tempTable} удалена.");
            }
            catch (Exception ex)
            {
                Log(
                    $"  Не удалось удалить {tempTable}:");

                Log(
                    ex.ToString());
            }
        }


        // ============================================================
        // ACCESS -> TEMP TABLE
        // ============================================================

        private void LoadAccessIntoTempTable(
            SqlConnection sqlConnection,
            string tableName,
            string tempTable,
            List<string> commonColumns,
            bool hasIdentity)
        {
            Log(
                $"  Загружаем Access -> {tempTable}...");

            string columnList =
                BuildColumnList(
                    commonColumns);

            string accessSql =
                $"SELECT {columnList} " +
                $"FROM [{tableName}]";

            using (var accessConnection =
                new OleDbConnection(
                    GetAccessConnectionString()))
            {
                accessConnection.Open();

                using (var accessCommand =
                    new OleDbCommand(
                        accessSql,
                        accessConnection))
                using (var rawReader =
                    accessCommand.ExecuteReader())
                // ФИКС №3/№5: тот же wrapper — конвертация Guid -> string
                // и подмена невалидных дат на NULL.
                using (var reader =
                    new AccessSafeDataReader(
                        rawReader,
                        Log))
                // ФИКС №6: КРИТИЧНО — SELECT TOP 0 * INTO копирует
                // свойство IDENTITY столбца Id во временную таблицу.
                // Без KeepIdentity SqlBulkCopy ИГНОРИРУЕТ значение Id
                // из Access и позволяет identity-столбцу временной
                // таблицы сгенерировать свой порядковый номер — реальный
                // Id из Access теряется уже на этом шаге, и дальше в
                // рабочую таблицу через IDENTITY_INSERT вставляется уже
                // не тот Id. Это ломает все дочерние таблицы, которые
                // ссылаются на исходный Access.Id (например OrderTarget).
                using (var bulk =
                    new SqlBulkCopy(
                        sqlConnection,
                        hasIdentity
                            ? SqlBulkCopyOptions.TableLock |
                              SqlBulkCopyOptions.KeepIdentity
                            : SqlBulkCopyOptions.TableLock,
                        null))
                {
                    bulk.DestinationTableName =
                        tempTable;

                    bulk.BatchSize =
                        1000;

                    bulk.NotifyAfter =
                        1000;

                    bulk.BulkCopyTimeout =
                        0;

                    bulk.EnableStreaming =
                        true;

                    bulk.SqlRowsCopied +=
                        (sender, args) =>
                        {
                            Log(
                                $"  {tableName}: " +
                                $"прочитано {args.RowsCopied} строк...");
                        };

                    foreach (string column in commonColumns)
                    {
                        bulk.ColumnMappings.Add(
                            column,
                            column);
                    }

                    bulk.WriteToServer(
                        reader);

                    reader.LogAnomalies(
                        tableName);
                }
            }

            Log(
                $"  Access -> {tempTable}: готово.");
        }


        // ============================================================
        // INSERT MISSING ROWS
        // ============================================================

        private int InsertMissingRows(
            SqlConnection connection,
            string tableName,
            string tempTable,
            List<string> commonColumns,
            List<string> pkColumns,
            bool hasIdentity)
        {
            Log(
                $"  Анализируем PK таблицы {tableName}...");

            Log(
                $"  PK: {string.Join(", ", pkColumns)}");

            string columnList =
                BuildColumnList(
                    commonColumns);

            string sourceAlias =
                "src";

            string targetAlias =
                "dst";

            string joinCondition =
                String.Join(
                    " AND ",
                    pkColumns.Select(
                        pk =>
                            $"{targetAlias}.[{pk}] = " +
                            $"{sourceAlias}.[{pk}]"));

            string insertColumns =
                String.Join(
                    ", ",
                    commonColumns.Select(
                        c =>
                            $"[{c}]"));

            string sourceColumns =
                String.Join(
                    ", ",
                    commonColumns.Select(
                        c =>
                            $"{sourceAlias}.[{c}]"));

            // ФИКС №1: SET IDENTITY_INSERT ON/OFF вокруг INSERT,
            // если у таблицы есть IDENTITY-столбец. Без этого
            // вставка явных значений PK (перенесённых из Access)
            // падает с ошибкой SQL 544.

            string identityOn =
                hasIdentity
                    ? $"SET IDENTITY_INSERT [dbo].[{tableName}] ON;"
                    : String.Empty;

            string identityOff =
                hasIdentity
                    ? $"SET IDENTITY_INSERT [dbo].[{tableName}] OFF;"
                    : String.Empty;

            string sql =
                $@"
{identityOn}

INSERT INTO [dbo].[{tableName}]
(
    {insertColumns}
)
SELECT
    {sourceColumns}
FROM {tempTable} {sourceAlias}
WHERE NOT EXISTS
(
    SELECT 1
    FROM [dbo].[{tableName}] {targetAlias}
    WHERE {joinCondition}
);

{identityOff}
";

            Log(
                "  Выполняем INSERT отсутствующих PK...");

            Log(
                $"  {sql}");

            using (var command =
                new SqlCommand(
                    sql,
                    connection))
            {
                command.CommandTimeout =
                    0;

                int inserted =
                    command.ExecuteNonQuery();

                // ExecuteNonQuery суммирует затронутые строки по
                // всем инструкциям в батче (SET IDENTITY_INSERT
                // строки затронутых не дают), так что значение
                // остаётся корректным количеством вставленных строк.

                Log(
                    $"  INSERT завершен. Добавлено: {inserted}");

                return inserted;
            }
        }


        // ============================================================
        // ORPHAN DIAGNOSTICS (без пропуска строк — только лог + стоп)
        // ============================================================

        /// <summary>
        /// Вызывается, когда CopyTable упала с нарушением FOREIGN KEY.
        /// Не пытается ничего перенести — только диагностирует:
        /// определяет все внешние ключи таблицы, для каждого считает,
        /// сколько строк-сирот он бы дал (значение FK, которого нет в
        /// таблице, на которую он ссылается), и логирует примеры PK
        /// таких строк. После этого CopyData бросает исключение и
        /// останавливает миграцию — строки-сироты не переносятся и не
        /// пропускаются молча, проблему нужно решить вручную в Access
        /// перед повторным запуском.
        /// </summary>
        private void LogOrphanDiagnostics(
            string tableName)
        {
            Log("");
            Log(
                $"Диагностика строк-сирот: {tableName}");

            List<string> accessColumns =
                GetAccessColumns(
                    tableName);

            List<string> sqlColumns =
                GetSqlColumns(
                    tableName);

            List<string> commonColumns =
                accessColumns
                    .Intersect(
                        sqlColumns,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            if (commonColumns.Count == 0)
            {
                Log(
                    "  Общих полей нет, диагностика невозможна.");

                return;
            }

            List<string> pkColumns;

            bool hasIdentity;

            List<(string Column, string RefTable, string RefColumn)> foreignKeys;

            using (var connection =
                new SqlConnection(
                    _sqlConnectionString))
            {
                connection.Open();

                pkColumns =
                    GetPrimaryKeyColumns(
                        connection,
                        tableName);

                hasIdentity =
                    HasIdentityColumn(
                        connection,
                        tableName);

                foreignKeys =
                    GetForeignKeys(
                        connection,
                        tableName);
            }

            foreignKeys =
                foreignKeys
                    .Where(
                        fk =>
                            commonColumns.Contains(
                                fk.Column,
                                StringComparer.OrdinalIgnoreCase))
                    .ToList();

            if (pkColumns.Count == 0)
            {
                Log(
                    "  PRIMARY KEY отсутствует, диагностика по PK " +
                    "невозможна.");

                return;
            }

            if (foreignKeys.Count == 0)
            {
                Log(
                    "  Внешних ключей не найдено — ошибка, судя по " +
                    "всему, не связана с сиротами.");

                return;
            }

            foreach (var fk in foreignKeys)
            {
                Log(
                    $"  FK: [{fk.Column}] -> " +
                    $"[{fk.RefTable}].[{fk.RefColumn}]");
            }

            string tempTable =
                BuildTempTableName(
                    tableName) +
                "_Diag";

            using (var sqlConnection =
                new SqlConnection(
                    _sqlConnectionString))
            {
                sqlConnection.Open();

                try
                {
                    CreateTempTable(
                        sqlConnection,
                        tableName,
                        tempTable);

                    LoadAccessIntoTempTable(
                        sqlConnection,
                        tableName,
                        tempTable,
                        commonColumns,
                        hasIdentity);

                    LogOrphanBreakdown(
                        sqlConnection,
                        tableName,
                        tempTable,
                        pkColumns,
                        foreignKeys);
                }
                finally
                {
                    DropTempTable(
                        sqlConnection,
                        tempTable);
                }
            }
        }


        private void LogOrphanBreakdown(
            SqlConnection connection,
            string tableName,
            string tempTable,
            List<string> pkColumns,
            List<(string Column, string RefTable, string RefColumn)> foreignKeys)
        {
            string sourceAlias =
                "src";

            string orphanCondition =
                String.Join(
                    " AND ",
                    foreignKeys.Select(
                        fk =>
                            $"(" +
                            $"{sourceAlias}.[{fk.Column}] IS NULL " +
                            $"OR EXISTS (" +
                            $"SELECT 1 FROM [dbo].[{fk.RefTable}] rf " +
                            $"WHERE rf.[{fk.RefColumn}] = " +
                            $"{sourceAlias}.[{fk.Column}]" +
                            $")" +
                            $")"));

            string pkList =
                String.Join(
                    " + '|' + ",
                    pkColumns.Select(
                        pk =>
                            $"CAST({sourceAlias}.[{pk}] AS NVARCHAR(100))"));

            string orphanCountSql =
                $@"
SELECT COUNT(*)
FROM {tempTable} {sourceAlias}
WHERE NOT ({orphanCondition});
";

            int orphanCount;

            using (var command =
                new SqlCommand(
                    orphanCountSql,
                    connection))
            {
                command.CommandTimeout =
                    0;

                orphanCount =
                    Convert.ToInt32(
                        command.ExecuteScalar());
            }

            if (orphanCount == 0)
            {
                Log(
                    "  Строк-сирот в текущей выборке не найдено " +
                    "(ошибка вызвана чем-то другим).");

                return;
            }

            Log(
                $"  Строк-сирот (недостающий FK): {orphanCount}.");

            Log(
                "  Разбивка сирот по конкретным FK:");

            foreach (var fk in foreignKeys)
            {
                string perFkCountSql =
                    $@"
SELECT COUNT(*)
FROM {tempTable} {sourceAlias}
WHERE {sourceAlias}.[{fk.Column}] IS NOT NULL
AND NOT EXISTS
(
    SELECT 1
    FROM [dbo].[{fk.RefTable}] rf
    WHERE rf.[{fk.RefColumn}] = {sourceAlias}.[{fk.Column}]
);
";

                using (var perFkCommand =
                    new SqlCommand(
                        perFkCountSql,
                        connection))
                {
                    perFkCommand.CommandTimeout =
                        0;

                    int perFkCount =
                        Convert.ToInt32(
                            perFkCommand.ExecuteScalar());

                    if (perFkCount > 0)
                    {
                        Log(
                            $"    [{fk.Column}] -> " +
                            $"[{fk.RefTable}].[{fk.RefColumn}]: " +
                            $"{perFkCount} строк(и)");
                    }
                }
            }

            string orphanSampleSql =
                $@"
SELECT TOP 20
    {pkList} AS PkValues
FROM {tempTable} {sourceAlias}
WHERE NOT ({orphanCondition});
";

            using (var sampleCommand =
                new SqlCommand(
                    orphanSampleSql,
                    connection))
            {
                sampleCommand.CommandTimeout =
                    0;

                using (var reader =
                    sampleCommand.ExecuteReader())
                {
                    Log(
                        "  Пример PK строк-сирот (до 20 из них, " +
                        "формат " +
                        String.Join(
                            "|",
                            pkColumns) +
                        "):");

                    while (reader.Read())
                    {
                        Log(
                            $"    - {reader.GetString(0)}");
                    }
                }
            }
        }


        private List<(string Column, string RefTable, string RefColumn)> GetForeignKeys(
            SqlConnection connection,
            string tableName)
        {
            const string sql =
@"
SELECT
    kcu1.COLUMN_NAME  AS FkColumn,
    kcu2.TABLE_NAME   AS RefTable,
    kcu2.COLUMN_NAME  AS RefColumn
FROM
    INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
INNER JOIN
    INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu1
        ON kcu1.CONSTRAINT_NAME =
           rc.CONSTRAINT_NAME
INNER JOIN
    INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu2
        ON kcu2.CONSTRAINT_NAME =
           rc.UNIQUE_CONSTRAINT_NAME
       AND kcu2.ORDINAL_POSITION =
           kcu1.ORDINAL_POSITION
WHERE
    kcu1.TABLE_SCHEMA = 'dbo'
    AND kcu1.TABLE_NAME = @TableName
ORDER BY
    kcu1.ORDINAL_POSITION;
";

            var result =
                new List<(string, string, string)>();

            using (var command =
                new SqlCommand(
                    sql,
                    connection))
            {
                command.CommandTimeout =
                    0;

                command.Parameters.Add(
                    "@TableName",
                    SqlDbType.NVarChar,
                    128).Value =
                    tableName;

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(
                            (
                                reader.GetString(0),
                                reader.GetString(1),
                                reader.GetString(2)
                            ));
                    }
                }
            }

            return result;
        }


        // ============================================================
        // PRIMARY KEY
        // ============================================================

        private List<string> GetPrimaryKeyColumns(
            SqlConnection connection,
            string tableName)
        {
            const string sql =
@"
SELECT
    c.COLUMN_NAME
FROM
    INFORMATION_SCHEMA.KEY_COLUMN_USAGE c
INNER JOIN
    INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
        ON tc.CONSTRAINT_NAME =
           c.CONSTRAINT_NAME
       AND tc.TABLE_NAME =
           c.TABLE_NAME
       AND tc.TABLE_SCHEMA =
           c.TABLE_SCHEMA
WHERE
    tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
    AND c.TABLE_NAME = @TableName
    AND c.TABLE_SCHEMA = 'dbo'
ORDER BY
    c.ORDINAL_POSITION;
";

            using (var command =
                new SqlCommand(
                    sql,
                    connection))
            {
                command.CommandTimeout =
                    0;

                command.Parameters.Add(
                    "@TableName",
                    SqlDbType.NVarChar,
                    128).Value =
                    tableName;

                var result =
                    new List<string>();

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(
                            reader.GetString(0));
                    }
                }

                return result;
            }
        }


        // ============================================================
        // COLUMNS
        // ============================================================

        private List<string> GetSqlColumns(
            string tableName)
        {
            var result =
                new List<string>();

            using (var connection =
                new SqlConnection(
                    _sqlConnectionString))
            {
                connection.Open();

                const string sql =
@"
SELECT
    COLUMN_NAME
FROM
    INFORMATION_SCHEMA.COLUMNS
WHERE
    TABLE_SCHEMA = 'dbo'
    AND TABLE_NAME = @TableName
ORDER BY
    ORDINAL_POSITION;
";

                using (var command =
                    new SqlCommand(
                        sql,
                        connection))
                {
                    command.Parameters.Add(
                        "@TableName",
                        SqlDbType.NVarChar,
                        128).Value =
                        tableName;

                    using (var reader =
                        command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(
                                reader.GetString(0));
                        }
                    }
                }
            }

            return result;
        }


        private List<string> GetAccessColumns(
            string tableName)
        {
            var result =
                new List<string>();

            using (var connection =
                new OleDbConnection(
                    GetAccessConnectionString()))
            {
                connection.Open();

                using (var command =
                    new OleDbCommand(
                        $"SELECT * FROM [{tableName}] WHERE 1 = 0",
                        connection))
                using (var reader =
                    command.ExecuteReader())
                {
                    DataTable schema =
                        reader.GetSchemaTable();

                    if (schema == null)
                    {
                        return result;
                    }

                    foreach (DataRow row in schema.Rows)
                    {
                        string columnName =
                            row["ColumnName"]
                                .ToString();

                        if (!result.Contains(
                            columnName,
                            StringComparer.OrdinalIgnoreCase))
                        {
                            result.Add(
                                columnName);
                        }
                    }
                }
            }

            return result;
        }


        // ============================================================
        // ACCESS TABLES
        // ============================================================

        private List<string> GetAccessTables()
        {
            var result =
                new List<string>();

            using (var connection =
                new OleDbConnection(
                    GetAccessConnectionString()))
            {
                connection.Open();

                DataTable schema =
                    connection.GetOleDbSchemaTable(
                        OleDbSchemaGuid.Tables,
                        new object[]
                        {
                            null,
                            null,
                            null,
                            "TABLE"
                        });

                if (schema == null)
                {
                    return result;
                }

                foreach (DataRow row in schema.Rows)
                {
                    string tableName =
                        row["TABLE_NAME"]
                            .ToString();

                    if (String.IsNullOrWhiteSpace(
                        tableName))
                    {
                        continue;
                    }

                    if (tableName.StartsWith(
                        "MSys",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!result.Contains(
                        tableName,
                        StringComparer.OrdinalIgnoreCase))
                    {
                        result.Add(
                            tableName);
                    }
                }
            }

            result.Sort(
                StringComparer.OrdinalIgnoreCase);

            return result;
        }


        // ============================================================
        // IDENTITY
        // ============================================================

        private bool HasIdentityColumn(
            SqlConnection connection,
            string tableName)
        {
            const string sql =
@"
SELECT COUNT(*)
FROM sys.identity_columns
WHERE object_id =
      OBJECT_ID(
          N'dbo.' + @TableName,
          N'U');
";

            using (var command =
                new SqlCommand(
                    sql,
                    connection))
            {
                command.Parameters.Add(
                    "@TableName",
                    SqlDbType.NVarChar,
                    128).Value =
                    tableName;

                return
                    Convert.ToInt32(
                        command.ExecuteScalar()) > 0;
            }
        }


        // ============================================================
        // HELPERS
        // ============================================================

        private string BuildColumnList(
            IEnumerable<string> columns)
        {
            return
                String.Join(
                    ", ",
                    columns.Select(
                        c =>
                            $"[{c}]"));
        }


        private string GetAccessConnectionString()
        {
            return
                $"Provider=Microsoft.Jet.OLEDB.4.0;" +
                $"Data Source={_config.MdbPath};";
        }


        // ============================================================
        // LOG
        // ============================================================

        private void InitializeLog()
        {
            string logsFolder =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "logs");

            Directory.CreateDirectory(
                logsFolder);

            _logFilePath =
                Path.Combine(
                    logsFolder,
                    $"Migration_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
        }


        private void Log(
            string text)
        {
            string line =
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {text}";

            System.Diagnostics.Debug.WriteLine(
                line);

            lock (_logLock)
            {
                File.AppendAllText(
                    _logFilePath,
                    line +
                    Environment.NewLine);
            }
        }


        // ============================================================
        // SAFE DATA READER: GUID -> STRING, INVALID DATES -> NULL
        // (ФИКС №3 и №5)
        // ============================================================

        /// <summary>
        /// Оборачивает IDataReader от Access и "лечит" два вида
        /// значений, которые SqlBulkCopy не может записать как есть:
        ///
        /// 1. Guid -> string (в верхнем регистре), если столбец
        ///    назначения на самом деле varchar/nvarchar, а не
        ///    uniqueidentifier.
        ///
        /// 2. DateTime вне диапазона, который понимает SQL Server
        ///    тип datetime (1/1/1753 .. 12/31/9999) -> NULL. Access
        ///    такие даты хранить умеет (например, дефолтные/битые
        ///    значения вроде 0001-01-01), а SqlBulkCopy на них падает
        ///    с SqlTypeException и валит перенос всей таблицы.
        ///    Каждая такая замена считается по столбцам и после
        ///    переноса таблицы выводится в лог методом LogAnomalies,
        ///    чтобы аномалию можно было найти и разобрать вручную.
        ///
        /// Все остальные методы просто делегируются исходному reader.
        /// </summary>
        private sealed class AccessSafeDataReader : IDataReader
        {
            private static readonly DateTime SqlDateTimeMin =
                new DateTime(1753, 1, 1);

            private static readonly DateTime SqlDateTimeMax =
                new DateTime(
                    9999, 12, 31, 23, 59, 59, 997);

            private readonly IDataReader _inner;

            private readonly Action<string> _log;

            private readonly HashSet<int> _guidColumns;

            private readonly Dictionary<int, int> _invalidDateCounts =
                new Dictionary<int, int>();

            public AccessSafeDataReader(
                IDataReader inner,
                Action<string> log)
            {
                _inner =
                    inner ??
                    throw new ArgumentNullException(
                        nameof(inner));

                _log =
                    log ??
                    throw new ArgumentNullException(
                        nameof(log));

                _guidColumns =
                    new HashSet<int>();

                for (int i = 0; i < _inner.FieldCount; i++)
                {
                    if (_inner.GetFieldType(i) == typeof(Guid))
                    {
                        _guidColumns.Add(i);
                    }
                }
            }

            private object Sanitize(
                int i,
                object value)
            {
                if (_guidColumns.Contains(i) &&
                    value is Guid guid)
                {
                    return
                        guid.ToString()
                            .ToUpperInvariant();
                }

                if (value is DateTime dateTime &&
                    (dateTime < SqlDateTimeMin ||
                     dateTime > SqlDateTimeMax))
                {
                    if (!_invalidDateCounts.ContainsKey(i))
                    {
                        _invalidDateCounts[i] =
                            0;
                    }

                    _invalidDateCounts[i]++;

                    return DBNull.Value;
                }

                return value;
            }

            /// <summary>
            /// Логирует, сколько невалидных дат было заменено на
            /// NULL, в разбивке по столбцам. Вызывать после
            /// WriteToServer, пока reader ещё не Dispose().
            /// </summary>
            public void LogAnomalies(
                string tableName)
            {
                foreach (var kvp in _invalidDateCounts)
                {
                    string columnName =
                        _inner.GetName(
                            kvp.Key);

                    _log(
                        $"  ВНИМАНИЕ: столбец [{columnName}] таблицы " +
                        $"{tableName} — {kvp.Value} значений даты вне " +
                        $"диапазона SQL Server (1753-9999). " +
                        $"Заменены на NULL.");
                }
            }

            public object GetValue(
                int i)
            {
                return
                    Sanitize(
                        i,
                        _inner.GetValue(i));
            }

            public int GetValues(
                object[] values)
            {
                int count =
                    _inner.GetValues(
                        values);

                for (int i = 0; i < count; i++)
                {
                    values[i] =
                        Sanitize(
                            i,
                            values[i]);
                }

                return count;
            }

            public Type GetFieldType(
                int i)
            {
                if (_guidColumns.Contains(i))
                {
                    return typeof(string);
                }

                return
                    _inner.GetFieldType(i);
            }

            public string GetDataTypeName(
                int i)
            {
                if (_guidColumns.Contains(i))
                {
                    return "string";
                }

                return
                    _inner.GetDataTypeName(i);
            }

            public string GetString(
                int i)
            {
                if (_guidColumns.Contains(i))
                {
                    object value =
                        _inner.GetValue(i);

                    if (value is Guid guid)
                    {
                        return
                            guid.ToString()
                                .ToUpperInvariant();
                    }

                    return
                        value?.ToString();
                }

                return
                    _inner.GetString(i);
            }

            // ---- всё остальное — прямое делегирование ----

            public int FieldCount =>
                _inner.FieldCount;

            public int Depth =>
                _inner.Depth;

            public bool IsClosed =>
                _inner.IsClosed;

            public int RecordsAffected =>
                _inner.RecordsAffected;

            public object this[int i] =>
                GetValue(i);

            public object this[string name] =>
                GetValue(
                    _inner.GetOrdinal(name));

            public void Close() =>
                _inner.Close();

            public void Dispose() =>
                _inner.Dispose();

            public bool NextResult() =>
                _inner.NextResult();

            public bool Read() =>
                _inner.Read();

            public DataTable GetSchemaTable() =>
                _inner.GetSchemaTable();

            public bool GetBoolean(int i) =>
                _inner.GetBoolean(i);

            public byte GetByte(int i) =>
                _inner.GetByte(i);

            public long GetBytes(
                int i,
                long fieldOffset,
                byte[] buffer,
                int bufferoffset,
                int length) =>
                _inner.GetBytes(
                    i,
                    fieldOffset,
                    buffer,
                    bufferoffset,
                    length);

            public char GetChar(int i) =>
                _inner.GetChar(i);

            public long GetChars(
                int i,
                long fieldoffset,
                char[] buffer,
                int bufferoffset,
                int length) =>
                _inner.GetChars(
                    i,
                    fieldoffset,
                    buffer,
                    bufferoffset,
                    length);

            public IDataReader GetData(int i) =>
                _inner.GetData(i);

            public Guid GetGuid(int i) =>
                _inner.GetGuid(i);

            public DateTime GetDateTime(int i) =>
                _inner.GetDateTime(i);

            public decimal GetDecimal(int i) =>
                _inner.GetDecimal(i);

            public double GetDouble(int i) =>
                _inner.GetDouble(i);

            public float GetFloat(int i) =>
                _inner.GetFloat(i);

            public short GetInt16(int i) =>
                _inner.GetInt16(i);

            public int GetInt32(int i) =>
                _inner.GetInt32(i);

            public long GetInt64(int i) =>
                _inner.GetInt64(i);

            public string GetName(int i) =>
                _inner.GetName(i);

            public int GetOrdinal(string name) =>
                _inner.GetOrdinal(name);

            public bool IsDBNull(int i) =>
                _inner.IsDBNull(i);
        }
    }
}


//using RRStoSQL.Helpers;
//using RRStoSQL.Models;
//using RRStoSQL.Services.Migration;

//using System;
//using System.Collections.Generic;
//using System.Data;
//using System.Data.OleDb;
//using System.Data.SqlClient;
//using System.IO;
//using System.Linq;

//namespace RRStoSQL.Services.Migration.Engines
//{
//    /// <summary>
//    /// Основной движок миграции Access -> SQL Server.
//    ///
//    /// Алгоритм:
//    ///
//    /// 1. Создание SQL базы из шаблона.
//    /// 2. Переключение конфигурации УР на SQL.
//    /// 3. Запуск УР.
//    /// 4. Ожидание окончания обновления структуры.
//    /// 5. Закрытие УР.
//    /// 6. Чтение таблиц Access.
//    /// 7. Для каждой таблицы (в несколько проходов, см. CopyData):
//    ///      Access -> временная SQL таблица
//    ///      временная таблица -> рабочая таблица
//    ///      вставляются только отсутствующие PK.
//    ///
//    /// Важный принцип:
//    /// данные из Access не должны безусловно вставляться
//    /// непосредственно в рабочую SQL таблицу.
//    ///
//    /// ИСПРАВЛЕНИЯ (2026-08-10):
//    ///
//    /// 1. IDENTITY_INSERT.
//    ///    Для таблиц со столбцом IDENTITY вставка явных значений PK
//    ///    требует SET IDENTITY_INSERT ON/OFF. Раньше это не делалось,
//    ///    из-за чего часть таблиц падала с ошибкой SQL 544.
//    ///
//    /// 2. Порядок таблиц / FOREIGN KEY.
//    ///    Таблицы обрабатывались в алфавитном порядке, а не в порядке
//    ///    зависимостей FK, из-за чего дочерние таблицы иногда
//    ///    вставлялись раньше родительских и падали по FK. Теперь
//    ///    порядок таблиц вычисляется один раз заранее топологической
//    ///    сортировкой по графу FK (GetTableProcessingOrder) —
//    ///    родительские таблицы гарантированно идут раньше дочерних,
//    ///    без многократных повторных проходов.
//    ///
//    /// 3. GUID -> VARCHAR.
//    ///    Access может хранить значение как Guid, а в SQL столбец
//    ///    имеет тип varchar/nvarchar/char. SqlBulkCopy не умеет
//    ///    конвертировать Guid в строку автоматически, поэтому
//    ///    поток данных из Access оборачивается в
//    ///    AccessSafeDataReader, который отдаёт такие значения
//    ///    уже как строки.
//    ///
//    /// 4. Строки-сироты (недостающий FK).
//    ///    Если таблица падает по FOREIGN KEY даже при правильном
//    ///    порядке — это, как правило, не порядок обработки, а
//    ///    реальные "осиротевшие" строки в Access (FK ссылается на
//    ///    запись, которой физически не существует). Такие строки НЕ
//    ///    переносятся и НЕ пропускаются молча: LogOrphanDiagnostics
//    ///    подробно логирует, какой именно FK и сколько строк виноваты
//    ///    (с примерами PK), после чего CopyData бросает исключение и
//    ///    останавливает миграцию целиком — до тех пор, пока проблема
//    ///    не будет разобрана и исправлена в Access вручную.
//    ///
//    /// 5. Даты вне диапазона SQL Server.
//    ///    SQL Server datetime понимает только 1/1/1753..12/31/9999,
//    ///    Access может хранить и более широкий диапазон (например,
//    ///    "битые" даты вроде 0001-01-01). AccessSafeDataReader
//    ///    подменяет такие значения на NULL вместо падения
//    ///    SqlBulkCopy, и логирует, в каком столбце и сколько раз
//    ///    это произошло.
//    /// </summary>
//    public class NativeMigrationEngine
//    {
//        private readonly SqlService _sqlService =
//            new SqlService();

//        private MigrationContext _context;

//        private AppConfig _config;

//        private string _databaseName;

//        private string _masterConnectionString;

//        private string _sqlConnectionString;

//        private string _templateFolder;

//        private string _sqlDataFolder;

//        private readonly object _logLock =
//            new object();

//        private string _logFilePath;


//        // ============================================================
//        // PUBLIC
//        // ============================================================

//        public void Run(
//            MigrationContext context)
//        {
//            if (context == null)
//            {
//                throw new ArgumentNullException(
//                    nameof(context));
//            }

//            if (context.Config == null)
//            {
//                throw new Exception(
//                    "Не загружена конфигурация.");
//            }

//            _context =
//                context;

//            _config =
//                context.Config;

//            InitializeLog();

//            Log("============================================");
//            Log("Запуск NativeMigrationEngine");
//            Log("============================================");

//            Initialize();

//            PrepareDatabase();

//            CopyData();

//            Log("============================================");
//            Log("Миграция завершена.");
//            Log("============================================");
//        }


//        // ============================================================
//        // INITIALIZE
//        // ============================================================

//        private void Initialize()
//        {
//            _databaseName =
//                _config.SqlDatabaseName;

//            _masterConnectionString =
//                $"Server={_config.SqlInstance};" +
//                $"Database=master;" +
//                $"Trusted_Connection=True;";

//            _sqlConnectionString =
//                $"Server={_config.SqlInstance};" +
//                $"Database={_databaseName};" +
//                $"Trusted_Connection=True;";

//            _templateFolder =
//                Path.Combine(
//                    ResourcePaths.Misc,
//                    "mssqlBlankBase");

//            _sqlDataFolder =
//                _sqlService.GetDefaultDataFolder(
//                    _config.SqlInstance);

//            Log($"SQL Instance: {_config.SqlInstance}");
//            Log($"SQL Database: {_databaseName}");
//            Log($"Access database: {_config.MdbPath}");
//            Log($"Template folder: {_templateFolder}");
//            Log($"SQL DATA folder: {_sqlDataFolder}");
//        }


//        // ============================================================
//        // DATABASE PREPARATION
//        // ============================================================

//        private void PrepareDatabase()
//        {
//            Log("--------------------------------------------");
//            Log("Подготовка SQL базы");
//            Log("--------------------------------------------");

//            CreateDatabaseFromTemplate();

//            Log("SQL база создана.");

//            SwitchRemoteRegToSql();

//            // ========================================================
//            // Выдаем RRUser права db_owner
//            // ========================================================

//            Log("Выдаём RRUser права db_owner...");

//            _sqlService.GrantDatabaseRights(
//                _config.SqlInstance,
//                _databaseName);

//            Log("RRUser получил права db_owner.");

//            StartRemoteReg();

//            WaitForDatabaseUpgrade();

//            CloseRemoteReg();

//            Log("Подготовка SQL базы завершена.");
//        }


//        private void CreateDatabaseFromTemplate()
//        {
//            Log("Создаем SQL базу из шаблона...");

//            _sqlService.CreateDatabaseFromTemplate(
//                _config.SqlInstance,
//                _databaseName,
//                _templateFolder);
//        }


//        private void SwitchRemoteRegToSql()
//        {
//            Log("Переключаем УР на SQL...");

//            var configService =
//                new ConfigService();

//            configService.SwitchToSql(
//                _context.ConfigPath,
//                _config);

//            Log("УР переключена на SQL.");
//        }


//        private RemoteRegController _remoteReg;

//        private void StartRemoteReg()
//        {
//            Log("Запускаем УР...");

//            _remoteReg =
//                new RemoteRegController();

//            _remoteReg.Start(
//                _config);

//            Log("УР запущена.");
//        }


//        private void WaitForDatabaseUpgrade()
//        {
//            Log("Ожидаем завершения обновления структуры...");

//            _sqlService.WaitForDatabaseUpgrade(
//                _config.SqlInstance,
//                _databaseName);

//            Log("Обновление структуры завершено.");
//        }


//        private void CloseRemoteReg()
//        {
//            if (_remoteReg == null)
//            {
//                return;
//            }

//            Log("Закрываем УР...");

//            _remoteReg.CloseGracefully();

//            _remoteReg = null;

//            Log("УР закрыта.");
//        }


//        // ============================================================
//        // DATA COPY
//        // ============================================================

//        private void CopyData()
//        {
//            Log("--------------------------------------------");
//            Log("Начинаем перенос данных");
//            Log("--------------------------------------------");

//            List<string> tables =
//                GetAccessTables();

//            Log(
//                $"Найдено таблиц Access: {tables.Count}");

//            // Порядок обработки считаем один раз заранее — по
//            // зависимостям FOREIGN KEY между таблицами (топологическая
//            // сортировка), а не по алфавиту. Это позволяет обойтись
//            // одним проходом вместо многократных повторов.

//            List<string> orderedTables =
//                GetTableProcessingOrder(
//                    tables);

//            foreach (string tableName in orderedTables)
//            {
//                try
//                {
//                    CopyTable(
//                        tableName);
//                }
//                catch (Exception ex)
//                {
//                    Log(
//                        $"ОШИБКА таблицы {tableName}:");

//                    Log(
//                        ex.ToString());

//                    // Если ошибка похожа на нарушение FOREIGN KEY —
//                    // строки-сироты в исходной Access базе (FK
//                    // ссылается на несуществующую запись). Такие
//                    // строки не пропускаются молча — диагностика
//                    // (какой именно FK и сколько строк) логируется
//                    // подробно, после чего миграция останавливается
//                    // целиком, чтобы проблему разобрали вручную.

//                    if (IsForeignKeyViolation(
//                        ex))
//                    {
//                        try
//                        {
//                            LogOrphanDiagnostics(
//                                tableName);
//                        }
//                        catch (Exception diagEx)
//                        {
//                            Log(
//                                $"Не удалось выполнить диагностику " +
//                                $"строк-сирот для {tableName}:");

//                            Log(
//                                diagEx.ToString());
//                        }
//                    }

//                    Log("--------------------------------------------");

//                    Log(
//                        $"Миграция остановлена: таблица {tableName} " +
//                        $"не перенесена (см. диагностику выше).");

//                    Log("--------------------------------------------");

//                    throw new Exception(
//                        $"Миграция остановлена на таблице {tableName}: " +
//                        $"{ex.Message}",
//                        ex);
//                }
//            }

//            Log("--------------------------------------------");
//            Log("Перенос данных завершен.");
//            Log("--------------------------------------------");
//        }


//        /// <summary>
//        /// Проверяет, является ли ошибка нарушением FOREIGN KEY
//        /// (SQL Server error 547). Проверка по номеру ошибки, а не по
//        /// тексту сообщения — не зависит от языка локализации SQL
//        /// Server.
//        /// </summary>
//        private bool IsForeignKeyViolation(
//            Exception ex)
//        {
//            return
//                ex is SqlException sqlEx &&
//                sqlEx.Number == 547;
//        }


//        // ============================================================
//        // TABLE PROCESSING ORDER (по зависимостям FK)
//        // ============================================================

//        /// <summary>
//        /// Строит порядок обработки таблиц так, чтобы таблица, на
//        /// которую ссылаются другие (родительская, по FK), переносилась
//        /// раньше дочерних. Обычная топологическая сортировка (DFS) по
//        /// графу внешних ключей, ограниченному только таблицами из
//        /// списка tables. Циклические зависимости FK (если есть)
//        /// не приводят к зависанию — такая таблица просто попадёт в
//        /// порядок как есть, а если из-за этого возникнет FK-ошибка,
//        /// её продиагностирует и остановит миграцию LogOrphanDiagnostics.
//        /// </summary>
//        private List<string> GetTableProcessingOrder(
//            List<string> tables)
//        {
//            Log(
//                "Определяем порядок таблиц по зависимостям FK...");

//            var tableSet =
//                new HashSet<string>(
//                    tables,
//                    StringComparer.OrdinalIgnoreCase);

//            var dependencies =
//                new Dictionary<string, HashSet<string>>(
//                    StringComparer.OrdinalIgnoreCase);

//            using (var connection =
//                new SqlConnection(
//                    _sqlConnectionString))
//            {
//                connection.Open();

//                foreach (string tableName in tables)
//                {
//                    List<(string Column, string RefTable, string RefColumn)> fks =
//                        GetForeignKeys(
//                            connection,
//                            tableName);

//                    var deps =
//                        new HashSet<string>(
//                            StringComparer.OrdinalIgnoreCase);

//                    foreach (var fk in fks)
//                    {
//                        if (tableSet.Contains(fk.RefTable) &&
//                            !string.Equals(
//                                fk.RefTable,
//                                tableName,
//                                StringComparison.OrdinalIgnoreCase))
//                        {
//                            deps.Add(
//                                fk.RefTable);
//                        }
//                    }

//                    dependencies[tableName] =
//                        deps;
//                }
//            }

//            var ordered =
//                new List<string>();

//            var visited =
//                new HashSet<string>(
//                    StringComparer.OrdinalIgnoreCase);

//            var visiting =
//                new HashSet<string>(
//                    StringComparer.OrdinalIgnoreCase);

//            void Visit(
//                string tableName)
//            {
//                if (visited.Contains(tableName))
//                {
//                    return;
//                }

//                if (visiting.Contains(tableName))
//                {
//                    // Циклическая зависимость FK — оставляем как есть,
//                    // дальше её вытянет спасательный проход.
//                    return;
//                }

//                visiting.Add(
//                    tableName);

//                foreach (string dep in dependencies[tableName]
//                    .OrderBy(
//                        x => x,
//                        StringComparer.OrdinalIgnoreCase))
//                {
//                    Visit(
//                        dep);
//                }

//                visiting.Remove(
//                    tableName);

//                visited.Add(
//                    tableName);

//                ordered.Add(
//                    tableName);
//            }

//            foreach (string tableName in tables
//                .OrderBy(
//                    x => x,
//                    StringComparer.OrdinalIgnoreCase))
//            {
//                Visit(
//                    tableName);
//            }

//            Log(
//                "Порядок таблиц определен.");

//            return ordered;
//        }


//        // ============================================================
//        // COPY ONE TABLE
//        // ============================================================

//        private void CopyTable(
//            string tableName)
//        {
//            Log("");
//            Log($"Таблица: {tableName}");

//            List<string> accessColumns =
//                GetAccessColumns(
//                    tableName);

//            List<string> sqlColumns =
//                GetSqlColumns(
//                    tableName);

//            Log(
//                $"  Access columns: {accessColumns.Count}");

//            Log(
//                $"  SQL columns: {sqlColumns.Count}");

//            List<string> commonColumns =
//                accessColumns
//                    .Intersect(
//                        sqlColumns,
//                        StringComparer.OrdinalIgnoreCase)
//                    .ToList();

//            if (commonColumns.Count == 0)
//            {
//                Log(
//                    "  Общих полей нет. Таблица пропущена.");

//                return;
//            }

//            List<string> missingInSql =
//                accessColumns
//                    .Except(
//                        sqlColumns,
//                        StringComparer.OrdinalIgnoreCase)
//                    .ToList();

//            foreach (string column in missingInSql)
//            {
//                Log(
//                    $"  Поле Access отсутствует в SQL: {column}");
//            }

//            List<string> pkColumns;

//            bool hasIdentity;

//            using (var connection =
//                new SqlConnection(
//                    _sqlConnectionString))
//            {
//                connection.Open();

//                pkColumns =
//                    GetPrimaryKeyColumns(
//                        connection,
//                        tableName);

//                hasIdentity =
//                    HasIdentityColumn(
//                        connection,
//                        tableName);
//            }

//            if (pkColumns.Count == 0)
//            {
//                Log(
//                    "  PRIMARY KEY отсутствует.");

//                CopyTableWithoutPrimaryKey(
//                    tableName,
//                    commonColumns,
//                    hasIdentity);

//                return;
//            }

//            Log(
//                $"  PRIMARY KEY: {string.Join(", ", pkColumns)}");

//            if (hasIdentity)
//            {
//                Log(
//                    "  Обнаружен IDENTITY столбец.");
//            }

//            int inserted =
//                CopyTableWithPrimaryKey(
//                    tableName,
//                    commonColumns,
//                    pkColumns,
//                    hasIdentity);

//            Log(
//                $"  Итог: вставлено {inserted} строк.");

//            Log(
//                "  Готово.");
//        }


//        // ============================================================
//        // TABLE WITHOUT PRIMARY KEY
//        // ============================================================

//        private void CopyTableWithoutPrimaryKey(
//            string tableName,
//            List<string> commonColumns,
//            bool hasIdentity)
//        {
//            Log(
//                "  Таблица без PK.");

//            Log(
//                "  Выполняем обычный перенос.");

//            string columnList =
//                BuildColumnList(
//                    commonColumns);

//            string accessSql =
//                $"SELECT {columnList} " +
//                $"FROM [{tableName}]";

//            using (var accessConnection =
//                new OleDbConnection(
//                    GetAccessConnectionString()))
//            using (var sqlConnection =
//                new SqlConnection(
//                    _sqlConnectionString))
//            {
//                accessConnection.Open();
//                sqlConnection.Open();

//                using (var command =
//                    new OleDbCommand(
//                        accessSql,
//                        accessConnection))
//                using (var rawReader =
//                    command.ExecuteReader())
//                // ФИКС №3/№5: оборачиваем reader, чтобы Guid отдавался
//                // как string, а даты вне диапазона SQL Server (1753-9999)
//                // подменялись на NULL вместо падения всей таблицы.
//                using (var reader =
//                    new AccessSafeDataReader(
//                        rawReader,
//                        Log))
//                using (var bulk =
//                    new SqlBulkCopy(
//                        sqlConnection,
//                        hasIdentity
//                            ? SqlBulkCopyOptions.TableLock |
//                              SqlBulkCopyOptions.KeepIdentity
//                            : SqlBulkCopyOptions.TableLock,
//                        null))
//                {
//                    bulk.DestinationTableName =
//                        $"[dbo].[{tableName}]";

//                    bulk.BatchSize =
//                        1000;

//                    bulk.NotifyAfter =
//                        1000;

//                    bulk.BulkCopyTimeout =
//                        0;

//                    bulk.EnableStreaming =
//                        true;

//                    bulk.SqlRowsCopied +=
//                        (sender, args) =>
//                        {
//                            Log(
//                                $"  {tableName}: " +
//                                $"{args.RowsCopied} строк...");
//                        };

//                    foreach (string column in commonColumns)
//                    {
//                        bulk.ColumnMappings.Add(
//                            column,
//                            column);
//                    }

//                    bulk.WriteToServer(
//                        reader);

//                    reader.LogAnomalies(
//                        tableName);
//                }
//            }
//        }


//        // ============================================================
//        // TABLE WITH PRIMARY KEY
//        // ============================================================

//        private int CopyTableWithPrimaryKey(
//            string tableName,
//            List<string> commonColumns,
//            List<string> pkColumns,
//            bool hasIdentity)
//        {
//            string tempTable =
//                BuildTempTableName(
//                    tableName);

//            using (var sqlConnection =
//                new SqlConnection(
//                    _sqlConnectionString))
//            {
//                sqlConnection.Open();

//                try
//                {
//                    CreateTempTable(
//                        sqlConnection,
//                        tableName,
//                        tempTable);

//                    LoadAccessIntoTempTable(
//                        sqlConnection,
//                        tableName,
//                        tempTable,
//                        commonColumns);

//                    int inserted =
//                        InsertMissingRows(
//                            sqlConnection,
//                            tableName,
//                            tempTable,
//                            commonColumns,
//                            pkColumns,
//                            hasIdentity);

//                    return inserted;
//                }
//                finally
//                {
//                    DropTempTable(
//                        sqlConnection,
//                        tempTable);
//                }
//            }
//        }


//        // ============================================================
//        // TEMP TABLE
//        // ============================================================

//        private string BuildTempTableName(
//            string tableName)
//        {
//            string safeName =
//                new string(
//                    tableName
//                        .Where(
//                            c =>
//                                char.IsLetterOrDigit(c) ||
//                                c == '_')
//                        .ToArray());

//            if (String.IsNullOrWhiteSpace(
//                safeName))
//            {
//                safeName =
//                    "Table";
//            }

//            return
//                $"#RRStoSQL_{safeName}";
//        }


//        private void CreateTempTable(
//            SqlConnection connection,
//            string tableName,
//            string tempTable)
//        {
//            Log(
//                $"  Создаем временную таблицу {tempTable}...");

//            string sql =
//                $@"
//SELECT TOP 0 *
//INTO {tempTable}
//FROM [dbo].[{tableName}];
//";

//            using (var command =
//                new SqlCommand(
//                    sql,
//                    connection))
//            {
//                command.CommandTimeout =
//                    0;

//                command.ExecuteNonQuery();
//            }
//        }


//        private void DropTempTable(
//            SqlConnection connection,
//            string tempTable)
//        {
//            try
//            {
//                string sql =
//                    $@"
//IF OBJECT_ID('tempdb..{tempTable.TrimStart('#')}')
//    IS NOT NULL
//    DROP TABLE {tempTable};
//";

//                using (var command =
//                    new SqlCommand(
//                        sql,
//                        connection))
//                {
//                    command.CommandTimeout =
//                        0;

//                    command.ExecuteNonQuery();
//                }

//                Log(
//                    $"  Временная таблица {tempTable} удалена.");
//            }
//            catch (Exception ex)
//            {
//                Log(
//                    $"  Не удалось удалить {tempTable}:");

//                Log(
//                    ex.ToString());
//            }
//        }


//        // ============================================================
//        // ACCESS -> TEMP TABLE
//        // ============================================================

//        private void LoadAccessIntoTempTable(
//            SqlConnection sqlConnection,
//            string tableName,
//            string tempTable,
//            List<string> commonColumns)
//        {
//            Log(
//                $"  Загружаем Access -> {tempTable}...");

//            string columnList =
//                BuildColumnList(
//                    commonColumns);

//            string accessSql =
//                $"SELECT {columnList} " +
//                $"FROM [{tableName}]";

//            using (var accessConnection =
//                new OleDbConnection(
//                    GetAccessConnectionString()))
//            {
//                accessConnection.Open();

//                using (var accessCommand =
//                    new OleDbCommand(
//                        accessSql,
//                        accessConnection))
//                using (var rawReader =
//                    accessCommand.ExecuteReader())
//                // ФИКС №3/№5: тот же wrapper — конвертация Guid -> string
//                // и подмена невалидных дат на NULL.
//                using (var reader =
//                    new AccessSafeDataReader(
//                        rawReader,
//                        Log))
//                using (var bulk =
//                    new SqlBulkCopy(
//                        sqlConnection,
//                        SqlBulkCopyOptions.TableLock,
//                        null))
//                {
//                    bulk.DestinationTableName =
//                        tempTable;

//                    bulk.BatchSize =
//                        1000;

//                    bulk.NotifyAfter =
//                        1000;

//                    bulk.BulkCopyTimeout =
//                        0;

//                    bulk.EnableStreaming =
//                        true;

//                    bulk.SqlRowsCopied +=
//                        (sender, args) =>
//                        {
//                            Log(
//                                $"  {tableName}: " +
//                                $"прочитано {args.RowsCopied} строк...");
//                        };

//                    foreach (string column in commonColumns)
//                    {
//                        bulk.ColumnMappings.Add(
//                            column,
//                            column);
//                    }

//                    bulk.WriteToServer(
//                        reader);

//                    reader.LogAnomalies(
//                        tableName);
//                }
//            }

//            Log(
//                $"  Access -> {tempTable}: готово.");
//        }


//        // ============================================================
//        // INSERT MISSING ROWS
//        // ============================================================

//        private int InsertMissingRows(
//            SqlConnection connection,
//            string tableName,
//            string tempTable,
//            List<string> commonColumns,
//            List<string> pkColumns,
//            bool hasIdentity)
//        {
//            Log(
//                $"  Анализируем PK таблицы {tableName}...");

//            Log(
//                $"  PK: {string.Join(", ", pkColumns)}");

//            string columnList =
//                BuildColumnList(
//                    commonColumns);

//            string sourceAlias =
//                "src";

//            string targetAlias =
//                "dst";

//            string joinCondition =
//                String.Join(
//                    " AND ",
//                    pkColumns.Select(
//                        pk =>
//                            $"{targetAlias}.[{pk}] = " +
//                            $"{sourceAlias}.[{pk}]"));

//            string insertColumns =
//                String.Join(
//                    ", ",
//                    commonColumns.Select(
//                        c =>
//                            $"[{c}]"));

//            string sourceColumns =
//                String.Join(
//                    ", ",
//                    commonColumns.Select(
//                        c =>
//                            $"{sourceAlias}.[{c}]"));

//            // ФИКС №1: SET IDENTITY_INSERT ON/OFF вокруг INSERT,
//            // если у таблицы есть IDENTITY-столбец. Без этого
//            // вставка явных значений PK (перенесённых из Access)
//            // падает с ошибкой SQL 544.

//            string identityOn =
//                hasIdentity
//                    ? $"SET IDENTITY_INSERT [dbo].[{tableName}] ON;"
//                    : String.Empty;

//            string identityOff =
//                hasIdentity
//                    ? $"SET IDENTITY_INSERT [dbo].[{tableName}] OFF;"
//                    : String.Empty;

//            string sql =
//                $@"
//{identityOn}

//INSERT INTO [dbo].[{tableName}]
//(
//    {insertColumns}
//)
//SELECT
//    {sourceColumns}
//FROM {tempTable} {sourceAlias}
//WHERE NOT EXISTS
//(
//    SELECT 1
//    FROM [dbo].[{tableName}] {targetAlias}
//    WHERE {joinCondition}
//);

//{identityOff}
//";

//            Log(
//                "  Выполняем INSERT отсутствующих PK...");

//            Log(
//                $"  {sql}");

//            using (var command =
//                new SqlCommand(
//                    sql,
//                    connection))
//            {
//                command.CommandTimeout =
//                    0;

//                int inserted =
//                    command.ExecuteNonQuery();

//                // ExecuteNonQuery суммирует затронутые строки по
//                // всем инструкциям в батче (SET IDENTITY_INSERT
//                // строки затронутых не дают), так что значение
//                // остаётся корректным количеством вставленных строк.

//                Log(
//                    $"  INSERT завершен. Добавлено: {inserted}");

//                return inserted;
//            }
//        }


//        // ============================================================
//        // ORPHAN DIAGNOSTICS (без пропуска строк — только лог + стоп)
//        // ============================================================

//        /// <summary>
//        /// Вызывается, когда CopyTable упала с нарушением FOREIGN KEY.
//        /// Не пытается ничего перенести — только диагностирует:
//        /// определяет все внешние ключи таблицы, для каждого считает,
//        /// сколько строк-сирот он бы дал (значение FK, которого нет в
//        /// таблице, на которую он ссылается), и логирует примеры PK
//        /// таких строк. После этого CopyData бросает исключение и
//        /// останавливает миграцию — строки-сироты не переносятся и не
//        /// пропускаются молча, проблему нужно решить вручную в Access
//        /// перед повторным запуском.
//        /// </summary>
//        private void LogOrphanDiagnostics(
//            string tableName)
//        {
//            Log("");
//            Log(
//                $"Диагностика строк-сирот: {tableName}");

//            List<string> accessColumns =
//                GetAccessColumns(
//                    tableName);

//            List<string> sqlColumns =
//                GetSqlColumns(
//                    tableName);

//            List<string> commonColumns =
//                accessColumns
//                    .Intersect(
//                        sqlColumns,
//                        StringComparer.OrdinalIgnoreCase)
//                    .ToList();

//            if (commonColumns.Count == 0)
//            {
//                Log(
//                    "  Общих полей нет, диагностика невозможна.");

//                return;
//            }

//            List<string> pkColumns;

//            List<(string Column, string RefTable, string RefColumn)> foreignKeys;

//            using (var connection =
//                new SqlConnection(
//                    _sqlConnectionString))
//            {
//                connection.Open();

//                pkColumns =
//                    GetPrimaryKeyColumns(
//                        connection,
//                        tableName);

//                foreignKeys =
//                    GetForeignKeys(
//                        connection,
//                        tableName);
//            }

//            foreignKeys =
//                foreignKeys
//                    .Where(
//                        fk =>
//                            commonColumns.Contains(
//                                fk.Column,
//                                StringComparer.OrdinalIgnoreCase))
//                    .ToList();

//            if (pkColumns.Count == 0)
//            {
//                Log(
//                    "  PRIMARY KEY отсутствует, диагностика по PK " +
//                    "невозможна.");

//                return;
//            }

//            if (foreignKeys.Count == 0)
//            {
//                Log(
//                    "  Внешних ключей не найдено — ошибка, судя по " +
//                    "всему, не связана с сиротами.");

//                return;
//            }

//            foreach (var fk in foreignKeys)
//            {
//                Log(
//                    $"  FK: [{fk.Column}] -> " +
//                    $"[{fk.RefTable}].[{fk.RefColumn}]");
//            }

//            string tempTable =
//                BuildTempTableName(
//                    tableName) +
//                "_Diag";

//            using (var sqlConnection =
//                new SqlConnection(
//                    _sqlConnectionString))
//            {
//                sqlConnection.Open();

//                try
//                {
//                    CreateTempTable(
//                        sqlConnection,
//                        tableName,
//                        tempTable);

//                    LoadAccessIntoTempTable(
//                        sqlConnection,
//                        tableName,
//                        tempTable,
//                        commonColumns);

//                    LogOrphanBreakdown(
//                        sqlConnection,
//                        tableName,
//                        tempTable,
//                        pkColumns,
//                        foreignKeys);
//                }
//                finally
//                {
//                    DropTempTable(
//                        sqlConnection,
//                        tempTable);
//                }
//            }
//        }


//        private void LogOrphanBreakdown(
//            SqlConnection connection,
//            string tableName,
//            string tempTable,
//            List<string> pkColumns,
//            List<(string Column, string RefTable, string RefColumn)> foreignKeys)
//        {
//            string sourceAlias =
//                "src";

//            string orphanCondition =
//                String.Join(
//                    " AND ",
//                    foreignKeys.Select(
//                        fk =>
//                            $"(" +
//                            $"{sourceAlias}.[{fk.Column}] IS NULL " +
//                            $"OR EXISTS (" +
//                            $"SELECT 1 FROM [dbo].[{fk.RefTable}] rf " +
//                            $"WHERE rf.[{fk.RefColumn}] = " +
//                            $"{sourceAlias}.[{fk.Column}]" +
//                            $")" +
//                            $")"));

//            string pkList =
//                String.Join(
//                    " + '|' + ",
//                    pkColumns.Select(
//                        pk =>
//                            $"CAST({sourceAlias}.[{pk}] AS NVARCHAR(100))"));

//            string orphanCountSql =
//                $@"
//SELECT COUNT(*)
//FROM {tempTable} {sourceAlias}
//WHERE NOT ({orphanCondition});
//";

//            int orphanCount;

//            using (var command =
//                new SqlCommand(
//                    orphanCountSql,
//                    connection))
//            {
//                command.CommandTimeout =
//                    0;

//                orphanCount =
//                    Convert.ToInt32(
//                        command.ExecuteScalar());
//            }

//            if (orphanCount == 0)
//            {
//                Log(
//                    "  Строк-сирот в текущей выборке не найдено " +
//                    "(ошибка вызвана чем-то другим).");

//                return;
//            }

//            Log(
//                $"  Строк-сирот (недостающий FK): {orphanCount}.");

//            Log(
//                "  Разбивка сирот по конкретным FK:");

//            foreach (var fk in foreignKeys)
//            {
//                string perFkCountSql =
//                    $@"
//SELECT COUNT(*)
//FROM {tempTable} {sourceAlias}
//WHERE {sourceAlias}.[{fk.Column}] IS NOT NULL
//AND NOT EXISTS
//(
//    SELECT 1
//    FROM [dbo].[{fk.RefTable}] rf
//    WHERE rf.[{fk.RefColumn}] = {sourceAlias}.[{fk.Column}]
//);
//";

//                using (var perFkCommand =
//                    new SqlCommand(
//                        perFkCountSql,
//                        connection))
//                {
//                    perFkCommand.CommandTimeout =
//                        0;

//                    int perFkCount =
//                        Convert.ToInt32(
//                            perFkCommand.ExecuteScalar());

//                    if (perFkCount > 0)
//                    {
//                        Log(
//                            $"    [{fk.Column}] -> " +
//                            $"[{fk.RefTable}].[{fk.RefColumn}]: " +
//                            $"{perFkCount} строк(и)");
//                    }
//                }
//            }

//            string orphanSampleSql =
//                $@"
//SELECT TOP 20
//    {pkList} AS PkValues
//FROM {tempTable} {sourceAlias}
//WHERE NOT ({orphanCondition});
//";

//            using (var sampleCommand =
//                new SqlCommand(
//                    orphanSampleSql,
//                    connection))
//            {
//                sampleCommand.CommandTimeout =
//                    0;

//                using (var reader =
//                    sampleCommand.ExecuteReader())
//                {
//                    Log(
//                        "  Пример PK строк-сирот (до 20 из них, " +
//                        "формат " +
//                        String.Join(
//                            "|",
//                            pkColumns) +
//                        "):");

//                    while (reader.Read())
//                    {
//                        Log(
//                            $"    - {reader.GetString(0)}");
//                    }
//                }
//            }
//        }


//        private List<(string Column, string RefTable, string RefColumn)> GetForeignKeys(
//            SqlConnection connection,
//            string tableName)
//        {
//            const string sql =
//@"
//SELECT
//    kcu1.COLUMN_NAME  AS FkColumn,
//    kcu2.TABLE_NAME   AS RefTable,
//    kcu2.COLUMN_NAME  AS RefColumn
//FROM
//    INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
//INNER JOIN
//    INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu1
//        ON kcu1.CONSTRAINT_NAME =
//           rc.CONSTRAINT_NAME
//INNER JOIN
//    INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu2
//        ON kcu2.CONSTRAINT_NAME =
//           rc.UNIQUE_CONSTRAINT_NAME
//       AND kcu2.ORDINAL_POSITION =
//           kcu1.ORDINAL_POSITION
//WHERE
//    kcu1.TABLE_SCHEMA = 'dbo'
//    AND kcu1.TABLE_NAME = @TableName
//ORDER BY
//    kcu1.ORDINAL_POSITION;
//";

//            var result =
//                new List<(string, string, string)>();

//            using (var command =
//                new SqlCommand(
//                    sql,
//                    connection))
//            {
//                command.CommandTimeout =
//                    0;

//                command.Parameters.Add(
//                    "@TableName",
//                    SqlDbType.NVarChar,
//                    128).Value =
//                    tableName;

//                using (var reader =
//                    command.ExecuteReader())
//                {
//                    while (reader.Read())
//                    {
//                        result.Add(
//                            (
//                                reader.GetString(0),
//                                reader.GetString(1),
//                                reader.GetString(2)
//                            ));
//                    }
//                }
//            }

//            return result;
//        }


//        // ============================================================
//        // PRIMARY KEY
//        // ============================================================

//        private List<string> GetPrimaryKeyColumns(
//            SqlConnection connection,
//            string tableName)
//        {
//            const string sql =
//@"
//SELECT
//    c.COLUMN_NAME
//FROM
//    INFORMATION_SCHEMA.KEY_COLUMN_USAGE c
//INNER JOIN
//    INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
//        ON tc.CONSTRAINT_NAME =
//           c.CONSTRAINT_NAME
//       AND tc.TABLE_NAME =
//           c.TABLE_NAME
//       AND tc.TABLE_SCHEMA =
//           c.TABLE_SCHEMA
//WHERE
//    tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
//    AND c.TABLE_NAME = @TableName
//    AND c.TABLE_SCHEMA = 'dbo'
//ORDER BY
//    c.ORDINAL_POSITION;
//";

//            using (var command =
//                new SqlCommand(
//                    sql,
//                    connection))
//            {
//                command.CommandTimeout =
//                    0;

//                command.Parameters.Add(
//                    "@TableName",
//                    SqlDbType.NVarChar,
//                    128).Value =
//                    tableName;

//                var result =
//                    new List<string>();

//                using (var reader =
//                    command.ExecuteReader())
//                {
//                    while (reader.Read())
//                    {
//                        result.Add(
//                            reader.GetString(0));
//                    }
//                }

//                return result;
//            }
//        }


//        // ============================================================
//        // COLUMNS
//        // ============================================================

//        private List<string> GetSqlColumns(
//            string tableName)
//        {
//            var result =
//                new List<string>();

//            using (var connection =
//                new SqlConnection(
//                    _sqlConnectionString))
//            {
//                connection.Open();

//                const string sql =
//@"
//SELECT
//    COLUMN_NAME
//FROM
//    INFORMATION_SCHEMA.COLUMNS
//WHERE
//    TABLE_SCHEMA = 'dbo'
//    AND TABLE_NAME = @TableName
//ORDER BY
//    ORDINAL_POSITION;
//";

//                using (var command =
//                    new SqlCommand(
//                        sql,
//                        connection))
//                {
//                    command.Parameters.Add(
//                        "@TableName",
//                        SqlDbType.NVarChar,
//                        128).Value =
//                        tableName;

//                    using (var reader =
//                        command.ExecuteReader())
//                    {
//                        while (reader.Read())
//                        {
//                            result.Add(
//                                reader.GetString(0));
//                        }
//                    }
//                }
//            }

//            return result;
//        }


//        private List<string> GetAccessColumns(
//            string tableName)
//        {
//            var result =
//                new List<string>();

//            using (var connection =
//                new OleDbConnection(
//                    GetAccessConnectionString()))
//            {
//                connection.Open();

//                using (var command =
//                    new OleDbCommand(
//                        $"SELECT * FROM [{tableName}] WHERE 1 = 0",
//                        connection))
//                using (var reader =
//                    command.ExecuteReader())
//                {
//                    DataTable schema =
//                        reader.GetSchemaTable();

//                    if (schema == null)
//                    {
//                        return result;
//                    }

//                    foreach (DataRow row in schema.Rows)
//                    {
//                        string columnName =
//                            row["ColumnName"]
//                                .ToString();

//                        if (!result.Contains(
//                            columnName,
//                            StringComparer.OrdinalIgnoreCase))
//                        {
//                            result.Add(
//                                columnName);
//                        }
//                    }
//                }
//            }

//            return result;
//        }


//        // ============================================================
//        // ACCESS TABLES
//        // ============================================================

//        private List<string> GetAccessTables()
//        {
//            var result =
//                new List<string>();

//            using (var connection =
//                new OleDbConnection(
//                    GetAccessConnectionString()))
//            {
//                connection.Open();

//                DataTable schema =
//                    connection.GetOleDbSchemaTable(
//                        OleDbSchemaGuid.Tables,
//                        new object[]
//                        {
//                            null,
//                            null,
//                            null,
//                            "TABLE"
//                        });

//                if (schema == null)
//                {
//                    return result;
//                }

//                foreach (DataRow row in schema.Rows)
//                {
//                    string tableName =
//                        row["TABLE_NAME"]
//                            .ToString();

//                    if (String.IsNullOrWhiteSpace(
//                        tableName))
//                    {
//                        continue;
//                    }

//                    if (tableName.StartsWith(
//                        "MSys",
//                        StringComparison.OrdinalIgnoreCase))
//                    {
//                        continue;
//                    }

//                    if (!result.Contains(
//                        tableName,
//                        StringComparer.OrdinalIgnoreCase))
//                    {
//                        result.Add(
//                            tableName);
//                    }
//                }
//            }

//            result.Sort(
//                StringComparer.OrdinalIgnoreCase);

//            return result;
//        }


//        // ============================================================
//        // IDENTITY
//        // ============================================================

//        private bool HasIdentityColumn(
//            SqlConnection connection,
//            string tableName)
//        {
//            const string sql =
//@"
//SELECT COUNT(*)
//FROM sys.identity_columns
//WHERE object_id =
//      OBJECT_ID(
//          N'dbo.' + @TableName,
//          N'U');
//";

//            using (var command =
//                new SqlCommand(
//                    sql,
//                    connection))
//            {
//                command.Parameters.Add(
//                    "@TableName",
//                    SqlDbType.NVarChar,
//                    128).Value =
//                    tableName;

//                return
//                    Convert.ToInt32(
//                        command.ExecuteScalar()) > 0;
//            }
//        }


//        // ============================================================
//        // HELPERS
//        // ============================================================

//        private string BuildColumnList(
//            IEnumerable<string> columns)
//        {
//            return
//                String.Join(
//                    ", ",
//                    columns.Select(
//                        c =>
//                            $"[{c}]"));
//        }


//        private string GetAccessConnectionString()
//        {
//            return
//                $"Provider=Microsoft.Jet.OLEDB.4.0;" +
//                $"Data Source={_config.MdbPath};";
//        }


//        // ============================================================
//        // LOG
//        // ============================================================

//        private void InitializeLog()
//        {
//            string logsFolder =
//                Path.Combine(
//                    AppDomain.CurrentDomain.BaseDirectory,
//                    "logs");

//            Directory.CreateDirectory(
//                logsFolder);

//            _logFilePath =
//                Path.Combine(
//                    logsFolder,
//                    $"Migration_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
//        }


//        private void Log(
//            string text)
//        {
//            string line =
//                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {text}";

//            System.Diagnostics.Debug.WriteLine(
//                line);

//            lock (_logLock)
//            {
//                File.AppendAllText(
//                    _logFilePath,
//                    line +
//                    Environment.NewLine);
//            }
//        }


//        // ============================================================
//        // SAFE DATA READER: GUID -> STRING, INVALID DATES -> NULL
//        // (ФИКС №3 и №5)
//        // ============================================================

//        /// <summary>
//        /// Оборачивает IDataReader от Access и "лечит" два вида
//        /// значений, которые SqlBulkCopy не может записать как есть:
//        ///
//        /// 1. Guid -> string (в верхнем регистре), если столбец
//        ///    назначения на самом деле varchar/nvarchar, а не
//        ///    uniqueidentifier.
//        ///
//        /// 2. DateTime вне диапазона, который понимает SQL Server
//        ///    тип datetime (1/1/1753 .. 12/31/9999) -> NULL. Access
//        ///    такие даты хранить умеет (например, дефолтные/битые
//        ///    значения вроде 0001-01-01), а SqlBulkCopy на них падает
//        ///    с SqlTypeException и валит перенос всей таблицы.
//        ///    Каждая такая замена считается по столбцам и после
//        ///    переноса таблицы выводится в лог методом LogAnomalies,
//        ///    чтобы аномалию можно было найти и разобрать вручную.
//        ///
//        /// Все остальные методы просто делегируются исходному reader.
//        /// </summary>
//        private sealed class AccessSafeDataReader : IDataReader
//        {
//            private static readonly DateTime SqlDateTimeMin =
//                new DateTime(1753, 1, 1);

//            private static readonly DateTime SqlDateTimeMax =
//                new DateTime(
//                    9999, 12, 31, 23, 59, 59, 997);

//            private readonly IDataReader _inner;

//            private readonly Action<string> _log;

//            private readonly HashSet<int> _guidColumns;

//            private readonly Dictionary<int, int> _invalidDateCounts =
//                new Dictionary<int, int>();

//            public AccessSafeDataReader(
//                IDataReader inner,
//                Action<string> log)
//            {
//                _inner =
//                    inner ??
//                    throw new ArgumentNullException(
//                        nameof(inner));

//                _log =
//                    log ??
//                    throw new ArgumentNullException(
//                        nameof(log));

//                _guidColumns =
//                    new HashSet<int>();

//                for (int i = 0; i < _inner.FieldCount; i++)
//                {
//                    if (_inner.GetFieldType(i) == typeof(Guid))
//                    {
//                        _guidColumns.Add(i);
//                    }
//                }
//            }

//            private object Sanitize(
//                int i,
//                object value)
//            {
//                if (_guidColumns.Contains(i) &&
//                    value is Guid guid)
//                {
//                    return
//                        guid.ToString()
//                            .ToUpperInvariant();
//                }

//                if (value is DateTime dateTime &&
//                    (dateTime < SqlDateTimeMin ||
//                     dateTime > SqlDateTimeMax))
//                {
//                    if (!_invalidDateCounts.ContainsKey(i))
//                    {
//                        _invalidDateCounts[i] =
//                            0;
//                    }

//                    _invalidDateCounts[i]++;

//                    return DBNull.Value;
//                }

//                return value;
//            }

//            /// <summary>
//            /// Логирует, сколько невалидных дат было заменено на
//            /// NULL, в разбивке по столбцам. Вызывать после
//            /// WriteToServer, пока reader ещё не Dispose().
//            /// </summary>
//            public void LogAnomalies(
//                string tableName)
//            {
//                foreach (var kvp in _invalidDateCounts)
//                {
//                    string columnName =
//                        _inner.GetName(
//                            kvp.Key);

//                    _log(
//                        $"  ВНИМАНИЕ: столбец [{columnName}] таблицы " +
//                        $"{tableName} — {kvp.Value} значений даты вне " +
//                        $"диапазона SQL Server (1753-9999). " +
//                        $"Заменены на NULL.");
//                }
//            }

//            public object GetValue(
//                int i)
//            {
//                return
//                    Sanitize(
//                        i,
//                        _inner.GetValue(i));
//            }

//            public int GetValues(
//                object[] values)
//            {
//                int count =
//                    _inner.GetValues(
//                        values);

//                for (int i = 0; i < count; i++)
//                {
//                    values[i] =
//                        Sanitize(
//                            i,
//                            values[i]);
//                }

//                return count;
//            }

//            public Type GetFieldType(
//                int i)
//            {
//                if (_guidColumns.Contains(i))
//                {
//                    return typeof(string);
//                }

//                return
//                    _inner.GetFieldType(i);
//            }

//            public string GetDataTypeName(
//                int i)
//            {
//                if (_guidColumns.Contains(i))
//                {
//                    return "string";
//                }

//                return
//                    _inner.GetDataTypeName(i);
//            }

//            public string GetString(
//                int i)
//            {
//                if (_guidColumns.Contains(i))
//                {
//                    object value =
//                        _inner.GetValue(i);

//                    if (value is Guid guid)
//                    {
//                        return
//                            guid.ToString()
//                                .ToUpperInvariant();
//                    }

//                    return
//                        value?.ToString();
//                }

//                return
//                    _inner.GetString(i);
//            }

//            // ---- всё остальное — прямое делегирование ----

//            public int FieldCount =>
//                _inner.FieldCount;

//            public int Depth =>
//                _inner.Depth;

//            public bool IsClosed =>
//                _inner.IsClosed;

//            public int RecordsAffected =>
//                _inner.RecordsAffected;

//            public object this[int i] =>
//                GetValue(i);

//            public object this[string name] =>
//                GetValue(
//                    _inner.GetOrdinal(name));

//            public void Close() =>
//                _inner.Close();

//            public void Dispose() =>
//                _inner.Dispose();

//            public bool NextResult() =>
//                _inner.NextResult();

//            public bool Read() =>
//                _inner.Read();

//            public DataTable GetSchemaTable() =>
//                _inner.GetSchemaTable();

//            public bool GetBoolean(int i) =>
//                _inner.GetBoolean(i);

//            public byte GetByte(int i) =>
//                _inner.GetByte(i);

//            public long GetBytes(
//                int i,
//                long fieldOffset,
//                byte[] buffer,
//                int bufferoffset,
//                int length) =>
//                _inner.GetBytes(
//                    i,
//                    fieldOffset,
//                    buffer,
//                    bufferoffset,
//                    length);

//            public char GetChar(int i) =>
//                _inner.GetChar(i);

//            public long GetChars(
//                int i,
//                long fieldoffset,
//                char[] buffer,
//                int bufferoffset,
//                int length) =>
//                _inner.GetChars(
//                    i,
//                    fieldoffset,
//                    buffer,
//                    bufferoffset,
//                    length);

//            public IDataReader GetData(int i) =>
//                _inner.GetData(i);

//            public Guid GetGuid(int i) =>
//                _inner.GetGuid(i);

//            public DateTime GetDateTime(int i) =>
//                _inner.GetDateTime(i);

//            public decimal GetDecimal(int i) =>
//                _inner.GetDecimal(i);

//            public double GetDouble(int i) =>
//                _inner.GetDouble(i);

//            public float GetFloat(int i) =>
//                _inner.GetFloat(i);

//            public short GetInt16(int i) =>
//                _inner.GetInt16(i);

//            public int GetInt32(int i) =>
//                _inner.GetInt32(i);

//            public long GetInt64(int i) =>
//                _inner.GetInt64(i);

//            public string GetName(int i) =>
//                _inner.GetName(i);

//            public int GetOrdinal(string name) =>
//                _inner.GetOrdinal(name);

//            public bool IsDBNull(int i) =>
//                _inner.IsDBNull(i);
//        }
//    }
//}

////using RRStoSQL.Helpers;
////using RRStoSQL.Models;
////using RRStoSQL.Services.Migration;

////using System;
////using System.Collections.Generic;
////using System.Data;
////using System.Data.OleDb;
////using System.Data.SqlClient;
////using System.IO;
////using System.Linq;

////namespace RRStoSQL.Services.Migration.Engines
////{
////    /// <summary>
////    /// Основной движок миграции Access -> SQL Server.
////    ///
////    /// Алгоритм:
////    ///
////    /// 1. Создание SQL базы из шаблона.
////    /// 2. Переключение конфигурации УР на SQL.
////    /// 3. Запуск УР.
////    /// 4. Ожидание окончания обновления структуры.
////    /// 5. Закрытие УР.
////    /// 6. Чтение таблиц Access.
////    /// 7. Для каждой таблицы (в несколько проходов, см. CopyData):
////    ///      Access -> временная SQL таблица
////    ///      временная таблица -> рабочая таблица
////    ///      вставляются только отсутствующие PK.
////    ///
////    /// Важный принцип:
////    /// данные из Access не должны безусловно вставляться
////    /// непосредственно в рабочую SQL таблицу.
////    ///
////    /// ИСПРАВЛЕНИЯ (2026-08-10):
////    ///
////    /// 1. IDENTITY_INSERT.
////    ///    Для таблиц со столбцом IDENTITY вставка явных значений PK
////    ///    требует SET IDENTITY_INSERT ON/OFF. Раньше это не делалось,
////    ///    из-за чего часть таблиц падала с ошибкой SQL 544.
////    ///
////    /// 2. Порядок таблиц / FOREIGN KEY.
////    ///    Таблицы обрабатывались в алфавитном порядке, а не в порядке
////    ///    зависимостей FK, из-за чего дочерние таблицы иногда
////    ///    вставлялись раньше родительских и падали по FK. Теперь
////    ///    порядок таблиц вычисляется один раз заранее топологической
////    ///    сортировкой по графу FK (GetTableProcessingOrder) —
////    ///    родительские таблицы гарантированно идут раньше дочерних,
////    ///    без многократных повторных проходов.
////    ///
////    /// 3. GUID -> VARCHAR.
////    ///    Access может хранить значение как Guid, а в SQL столбец
////    ///    имеет тип varchar/nvarchar/char. SqlBulkCopy не умеет
////    ///    конвертировать Guid в строку автоматически, поэтому
////    ///    поток данных из Access оборачивается в
////    ///    AccessSafeDataReader, который отдаёт такие значения
////    ///    уже как строки.
////    ///
////    /// 4. Строки-сироты (недостающий FK).
////    ///    Если таблица падает по FOREIGN KEY даже при правильном
////    ///    порядке — это, как правило, не порядок обработки, а
////    ///    реальные "осиротевшие" строки в Access (FK ссылается на
////    ///    запись, которой физически не существует). Единожды, сразу
////    ///    после ошибки, CopyTableSkippingOrphans переносит всё
////    ///    валидное; пропущенные строки и конкретный виновный FK
////    ///    подробно логируются для ручного разбора.
////    ///
////    /// 5. Даты вне диапазона SQL Server.
////    ///    SQL Server datetime понимает только 1/1/1753..12/31/9999,
////    ///    Access может хранить и более широкий диапазон (например,
////    ///    "битые" даты вроде 0001-01-01). AccessSafeDataReader
////    ///    подменяет такие значения на NULL вместо падения
////    ///    SqlBulkCopy, и логирует, в каком столбце и сколько раз
////    ///    это произошло.
////    /// </summary>
////    public class NativeMigrationEngine
////    {
////        private readonly SqlService _sqlService =
////            new SqlService();

////        private MigrationContext _context;

////        private AppConfig _config;

////        private string _databaseName;

////        private string _masterConnectionString;

////        private string _sqlConnectionString;

////        private string _templateFolder;

////        private string _sqlDataFolder;

////        private readonly object _logLock =
////            new object();

////        private string _logFilePath;


////        // ============================================================
////        // PUBLIC
////        // ============================================================

////        public void Run(
////            MigrationContext context)
////        {
////            if (context == null)
////            {
////                throw new ArgumentNullException(
////                    nameof(context));
////            }

////            if (context.Config == null)
////            {
////                throw new Exception(
////                    "Не загружена конфигурация.");
////            }

////            _context =
////                context;

////            _config =
////                context.Config;

////            InitializeLog();

////            Log("============================================");
////            Log("Запуск NativeMigrationEngine");
////            Log("============================================");

////            Initialize();

////            PrepareDatabase();

////            CopyData();

////            Log("============================================");
////            Log("Миграция завершена.");
////            Log("============================================");
////        }


////        // ============================================================
////        // INITIALIZE
////        // ============================================================

////        private void Initialize()
////        {
////            _databaseName =
////                _config.SqlDatabaseName;

////            _masterConnectionString =
////                $"Server={_config.SqlInstance};" +
////                $"Database=master;" +
////                $"Trusted_Connection=True;";

////            _sqlConnectionString =
////                $"Server={_config.SqlInstance};" +
////                $"Database={_databaseName};" +
////                $"Trusted_Connection=True;";

////            _templateFolder =
////                Path.Combine(
////                    ResourcePaths.Misc,
////                    "mssqlBlankBase");

////            _sqlDataFolder =
////                _sqlService.GetDefaultDataFolder(
////                    _config.SqlInstance);

////            Log($"SQL Instance: {_config.SqlInstance}");
////            Log($"SQL Database: {_databaseName}");
////            Log($"Access database: {_config.MdbPath}");
////            Log($"Template folder: {_templateFolder}");
////            Log($"SQL DATA folder: {_sqlDataFolder}");
////        }


////        // ============================================================
////        // DATABASE PREPARATION
////        // ============================================================

////        private void PrepareDatabase()
////        {
////            Log("--------------------------------------------");
////            Log("Подготовка SQL базы");
////            Log("--------------------------------------------");

////            CreateDatabaseFromTemplate();

////            Log("SQL база создана.");

////            SwitchRemoteRegToSql();

////            StartRemoteReg();

////            WaitForDatabaseUpgrade();

////            CloseRemoteReg();

////            Log("Подготовка SQL базы завершена.");
////        }


////        private void CreateDatabaseFromTemplate()
////        {
////            Log("Создаем SQL базу из шаблона...");

////            _sqlService.CreateDatabaseFromTemplate(
////                _config.SqlInstance,
////                _databaseName,
////                _templateFolder);
////        }


////        private void SwitchRemoteRegToSql()
////        {
////            Log("Переключаем УР на SQL...");

////            var configService =
////                new ConfigService();

////            configService.SwitchToSql(
////                _context.ConfigPath,
////                _config);

////            Log("УР переключена на SQL.");
////        }


////        private RemoteRegController _remoteReg;

////        private void StartRemoteReg()
////        {
////            Log("Запускаем УР...");

////            _remoteReg =
////                new RemoteRegController();

////            _remoteReg.Start(
////                _config);

////            Log("УР запущена.");
////        }


////        private void WaitForDatabaseUpgrade()
////        {
////            Log("Ожидаем завершения обновления структуры...");

////            _sqlService.WaitForDatabaseUpgrade(
////                _config.SqlInstance,
////                _databaseName);

////            Log("Обновление структуры завершено.");
////        }


////        private void CloseRemoteReg()
////        {
////            if (_remoteReg == null)
////            {
////                return;
////            }

////            Log("Закрываем УР...");

////            _remoteReg.CloseGracefully();

////            _remoteReg = null;

////            Log("УР закрыта.");
////        }


////        // ============================================================
////        // DATA COPY
////        // ============================================================

////        private void CopyData()
////        {
////            Log("--------------------------------------------");
////            Log("Начинаем перенос данных");
////            Log("--------------------------------------------");

////            List<string> tables =
////                GetAccessTables();

////            Log(
////                $"Найдено таблиц Access: {tables.Count}");

////            // Порядок обработки считаем один раз заранее — по
////            // зависимостям FOREIGN KEY между таблицами (топологическая
////            // сортировка), а не по алфавиту. Это позволяет обойтись
////            // одним проходом вместо многократных повторов.

////            List<string> orderedTables =
////                GetTableProcessingOrder(
////                    tables);

////            foreach (string tableName in orderedTables)
////            {
////                try
////                {
////                    CopyTable(
////                        tableName);
////                }
////                catch (Exception ex)
////                {
////                    Log(
////                        $"ОШИБКА таблицы {tableName}:");

////                    Log(
////                        ex.ToString());

////                    // Если и при правильном порядке таблица не
////                    // перенеслась — скорее всего, это не порядок,
////                    // а строки-сироты в исходной Access базе (FK
////                    // ссылается на несуществующую запись). Пробуем
////                    // перенести всё валидное, сирот пропускаем и
////                    // логируем для разбора вручную.

////                    Log(
////                        $"Пробуем перенести {tableName} с пропуском " +
////                        $"строк-сирот (недостающий FK)...");

////                    try
////                    {
////                        CopyTableSkippingOrphans(
////                            tableName);
////                    }
////                    catch (Exception ex2)
////                    {
////                        Log(
////                            $"Не удалось перенести {tableName} " +
////                            $"даже с пропуском сирот:");

////                        Log(
////                            ex2.ToString());
////                    }
////                }
////            }

////            Log("--------------------------------------------");
////            Log("Перенос данных завершен.");
////            Log("--------------------------------------------");
////        }


////        // ============================================================
////        // TABLE PROCESSING ORDER (по зависимостям FK)
////        // ============================================================

////        /// <summary>
////        /// Строит порядок обработки таблиц так, чтобы таблица, на
////        /// которую ссылаются другие (родительская, по FK), переносилась
////        /// раньше дочерних. Обычная топологическая сортировка (DFS) по
////        /// графу внешних ключей, ограниченному только таблицами из
////        /// списка tables. Циклические зависимости FK (если есть)
////        /// не приводят к зависанию — такая таблица просто попадёт в
////        /// порядок как есть, а её вероятные проблемы разберёт
////        /// спасательный проход (CopyTableSkippingOrphans).
////        /// </summary>
////        private List<string> GetTableProcessingOrder(
////            List<string> tables)
////        {
////            Log(
////                "Определяем порядок таблиц по зависимостям FK...");

////            var tableSet =
////                new HashSet<string>(
////                    tables,
////                    StringComparer.OrdinalIgnoreCase);

////            var dependencies =
////                new Dictionary<string, HashSet<string>>(
////                    StringComparer.OrdinalIgnoreCase);

////            using (var connection =
////                new SqlConnection(
////                    _sqlConnectionString))
////            {
////                connection.Open();

////                foreach (string tableName in tables)
////                {
////                    List<(string Column, string RefTable, string RefColumn)> fks =
////                        GetForeignKeys(
////                            connection,
////                            tableName);

////                    var deps =
////                        new HashSet<string>(
////                            StringComparer.OrdinalIgnoreCase);

////                    foreach (var fk in fks)
////                    {
////                        if (tableSet.Contains(fk.RefTable) &&
////                            !string.Equals(
////                                fk.RefTable,
////                                tableName,
////                                StringComparison.OrdinalIgnoreCase))
////                        {
////                            deps.Add(
////                                fk.RefTable);
////                        }
////                    }

////                    dependencies[tableName] =
////                        deps;
////                }
////            }

////            var ordered =
////                new List<string>();

////            var visited =
////                new HashSet<string>(
////                    StringComparer.OrdinalIgnoreCase);

////            var visiting =
////                new HashSet<string>(
////                    StringComparer.OrdinalIgnoreCase);

////            void Visit(
////                string tableName)
////            {
////                if (visited.Contains(tableName))
////                {
////                    return;
////                }

////                if (visiting.Contains(tableName))
////                {
////                    // Циклическая зависимость FK — оставляем как есть,
////                    // дальше её вытянет спасательный проход.
////                    return;
////                }

////                visiting.Add(
////                    tableName);

////                foreach (string dep in dependencies[tableName]
////                    .OrderBy(
////                        x => x,
////                        StringComparer.OrdinalIgnoreCase))
////                {
////                    Visit(
////                        dep);
////                }

////                visiting.Remove(
////                    tableName);

////                visited.Add(
////                    tableName);

////                ordered.Add(
////                    tableName);
////            }

////            foreach (string tableName in tables
////                .OrderBy(
////                    x => x,
////                    StringComparer.OrdinalIgnoreCase))
////            {
////                Visit(
////                    tableName);
////            }

////            Log(
////                "Порядок таблиц определен.");

////            return ordered;
////        }


////        // ============================================================
////        // COPY ONE TABLE
////        // ============================================================

////        private void CopyTable(
////            string tableName)
////        {
////            Log("");
////            Log($"Таблица: {tableName}");

////            List<string> accessColumns =
////                GetAccessColumns(
////                    tableName);

////            List<string> sqlColumns =
////                GetSqlColumns(
////                    tableName);

////            Log(
////                $"  Access columns: {accessColumns.Count}");

////            Log(
////                $"  SQL columns: {sqlColumns.Count}");

////            List<string> commonColumns =
////                accessColumns
////                    .Intersect(
////                        sqlColumns,
////                        StringComparer.OrdinalIgnoreCase)
////                    .ToList();

////            if (commonColumns.Count == 0)
////            {
////                Log(
////                    "  Общих полей нет. Таблица пропущена.");

////                return;
////            }

////            List<string> missingInSql =
////                accessColumns
////                    .Except(
////                        sqlColumns,
////                        StringComparer.OrdinalIgnoreCase)
////                    .ToList();

////            foreach (string column in missingInSql)
////            {
////                Log(
////                    $"  Поле Access отсутствует в SQL: {column}");
////            }

////            List<string> pkColumns;

////            bool hasIdentity;

////            using (var connection =
////                new SqlConnection(
////                    _sqlConnectionString))
////            {
////                connection.Open();

////                pkColumns =
////                    GetPrimaryKeyColumns(
////                        connection,
////                        tableName);

////                hasIdentity =
////                    HasIdentityColumn(
////                        connection,
////                        tableName);
////            }

////            if (pkColumns.Count == 0)
////            {
////                Log(
////                    "  PRIMARY KEY отсутствует.");

////                CopyTableWithoutPrimaryKey(
////                    tableName,
////                    commonColumns,
////                    hasIdentity);

////                return;
////            }

////            Log(
////                $"  PRIMARY KEY: {string.Join(", ", pkColumns)}");

////            if (hasIdentity)
////            {
////                Log(
////                    "  Обнаружен IDENTITY столбец.");
////            }

////            int inserted =
////                CopyTableWithPrimaryKey(
////                    tableName,
////                    commonColumns,
////                    pkColumns,
////                    hasIdentity);

////            Log(
////                $"  Итог: вставлено {inserted} строк.");

////            Log(
////                "  Готово.");
////        }


////        // ============================================================
////        // TABLE WITHOUT PRIMARY KEY
////        // ============================================================

////        private void CopyTableWithoutPrimaryKey(
////            string tableName,
////            List<string> commonColumns,
////            bool hasIdentity)
////        {
////            Log(
////                "  Таблица без PK.");

////            Log(
////                "  Выполняем обычный перенос.");

////            string columnList =
////                BuildColumnList(
////                    commonColumns);

////            string accessSql =
////                $"SELECT {columnList} " +
////                $"FROM [{tableName}]";

////            using (var accessConnection =
////                new OleDbConnection(
////                    GetAccessConnectionString()))
////            using (var sqlConnection =
////                new SqlConnection(
////                    _sqlConnectionString))
////            {
////                accessConnection.Open();
////                sqlConnection.Open();

////                using (var command =
////                    new OleDbCommand(
////                        accessSql,
////                        accessConnection))
////                using (var rawReader =
////                    command.ExecuteReader())
////                // ФИКС №3/№5: оборачиваем reader, чтобы Guid отдавался
////                // как string, а даты вне диапазона SQL Server (1753-9999)
////                // подменялись на NULL вместо падения всей таблицы.
////                using (var reader =
////                    new AccessSafeDataReader(
////                        rawReader,
////                        Log))
////                using (var bulk =
////                    new SqlBulkCopy(
////                        sqlConnection,
////                        hasIdentity
////                            ? SqlBulkCopyOptions.TableLock |
////                              SqlBulkCopyOptions.KeepIdentity
////                            : SqlBulkCopyOptions.TableLock,
////                        null))
////                {
////                    bulk.DestinationTableName =
////                        $"[dbo].[{tableName}]";

////                    bulk.BatchSize =
////                        1000;

////                    bulk.NotifyAfter =
////                        1000;

////                    bulk.BulkCopyTimeout =
////                        0;

////                    bulk.EnableStreaming =
////                        true;

////                    bulk.SqlRowsCopied +=
////                        (sender, args) =>
////                        {
////                            Log(
////                                $"  {tableName}: " +
////                                $"{args.RowsCopied} строк...");
////                        };

////                    foreach (string column in commonColumns)
////                    {
////                        bulk.ColumnMappings.Add(
////                            column,
////                            column);
////                    }

////                    bulk.WriteToServer(
////                        reader);

////                    reader.LogAnomalies(
////                        tableName);
////                }
////            }
////        }


////        // ============================================================
////        // TABLE WITH PRIMARY KEY
////        // ============================================================

////        private int CopyTableWithPrimaryKey(
////            string tableName,
////            List<string> commonColumns,
////            List<string> pkColumns,
////            bool hasIdentity)
////        {
////            string tempTable =
////                BuildTempTableName(
////                    tableName);

////            using (var sqlConnection =
////                new SqlConnection(
////                    _sqlConnectionString))
////            {
////                sqlConnection.Open();

////                try
////                {
////                    CreateTempTable(
////                        sqlConnection,
////                        tableName,
////                        tempTable);

////                    LoadAccessIntoTempTable(
////                        sqlConnection,
////                        tableName,
////                        tempTable,
////                        commonColumns);

////                    int inserted =
////                        InsertMissingRows(
////                            sqlConnection,
////                            tableName,
////                            tempTable,
////                            commonColumns,
////                            pkColumns,
////                            hasIdentity);

////                    return inserted;
////                }
////                finally
////                {
////                    DropTempTable(
////                        sqlConnection,
////                        tempTable);
////                }
////            }
////        }


////        // ============================================================
////        // TEMP TABLE
////        // ============================================================

////        private string BuildTempTableName(
////            string tableName)
////        {
////            string safeName =
////                new string(
////                    tableName
////                        .Where(
////                            c =>
////                                char.IsLetterOrDigit(c) ||
////                                c == '_')
////                        .ToArray());

////            if (String.IsNullOrWhiteSpace(
////                safeName))
////            {
////                safeName =
////                    "Table";
////            }

////            return
////                $"#RRStoSQL_{safeName}";
////        }


////        private void CreateTempTable(
////            SqlConnection connection,
////            string tableName,
////            string tempTable)
////        {
////            Log(
////                $"  Создаем временную таблицу {tempTable}...");

////            string sql =
////                $@"
////SELECT TOP 0 *
////INTO {tempTable}
////FROM [dbo].[{tableName}];
////";

////            using (var command =
////                new SqlCommand(
////                    sql,
////                    connection))
////            {
////                command.CommandTimeout =
////                    0;

////                command.ExecuteNonQuery();
////            }
////        }


////        private void DropTempTable(
////            SqlConnection connection,
////            string tempTable)
////        {
////            try
////            {
////                string sql =
////                    $@"
////IF OBJECT_ID('tempdb..{tempTable.TrimStart('#')}')
////    IS NOT NULL
////    DROP TABLE {tempTable};
////";

////                using (var command =
////                    new SqlCommand(
////                        sql,
////                        connection))
////                {
////                    command.CommandTimeout =
////                        0;

////                    command.ExecuteNonQuery();
////                }

////                Log(
////                    $"  Временная таблица {tempTable} удалена.");
////            }
////            catch (Exception ex)
////            {
////                Log(
////                    $"  Не удалось удалить {tempTable}:");

////                Log(
////                    ex.ToString());
////            }
////        }


////        // ============================================================
////        // ACCESS -> TEMP TABLE
////        // ============================================================

////        private void LoadAccessIntoTempTable(
////            SqlConnection sqlConnection,
////            string tableName,
////            string tempTable,
////            List<string> commonColumns)
////        {
////            Log(
////                $"  Загружаем Access -> {tempTable}...");

////            string columnList =
////                BuildColumnList(
////                    commonColumns);

////            string accessSql =
////                $"SELECT {columnList} " +
////                $"FROM [{tableName}]";

////            using (var accessConnection =
////                new OleDbConnection(
////                    GetAccessConnectionString()))
////            {
////                accessConnection.Open();

////                using (var accessCommand =
////                    new OleDbCommand(
////                        accessSql,
////                        accessConnection))
////                using (var rawReader =
////                    accessCommand.ExecuteReader())
////                // ФИКС №3/№5: тот же wrapper — конвертация Guid -> string
////                // и подмена невалидных дат на NULL.
////                using (var reader =
////                    new AccessSafeDataReader(
////                        rawReader,
////                        Log))
////                using (var bulk =
////                    new SqlBulkCopy(
////                        sqlConnection,
////                        SqlBulkCopyOptions.TableLock,
////                        null))
////                {
////                    bulk.DestinationTableName =
////                        tempTable;

////                    bulk.BatchSize =
////                        1000;

////                    bulk.NotifyAfter =
////                        1000;

////                    bulk.BulkCopyTimeout =
////                        0;

////                    bulk.EnableStreaming =
////                        true;

////                    bulk.SqlRowsCopied +=
////                        (sender, args) =>
////                        {
////                            Log(
////                                $"  {tableName}: " +
////                                $"прочитано {args.RowsCopied} строк...");
////                        };

////                    foreach (string column in commonColumns)
////                    {
////                        bulk.ColumnMappings.Add(
////                            column,
////                            column);
////                    }

////                    bulk.WriteToServer(
////                        reader);

////                    reader.LogAnomalies(
////                        tableName);
////                }
////            }

////            Log(
////                $"  Access -> {tempTable}: готово.");
////        }


////        // ============================================================
////        // INSERT MISSING ROWS
////        // ============================================================

////        private int InsertMissingRows(
////            SqlConnection connection,
////            string tableName,
////            string tempTable,
////            List<string> commonColumns,
////            List<string> pkColumns,
////            bool hasIdentity)
////        {
////            Log(
////                $"  Анализируем PK таблицы {tableName}...");

////            Log(
////                $"  PK: {string.Join(", ", pkColumns)}");

////            string columnList =
////                BuildColumnList(
////                    commonColumns);

////            string sourceAlias =
////                "src";

////            string targetAlias =
////                "dst";

////            string joinCondition =
////                String.Join(
////                    " AND ",
////                    pkColumns.Select(
////                        pk =>
////                            $"{targetAlias}.[{pk}] = " +
////                            $"{sourceAlias}.[{pk}]"));

////            string insertColumns =
////                String.Join(
////                    ", ",
////                    commonColumns.Select(
////                        c =>
////                            $"[{c}]"));

////            string sourceColumns =
////                String.Join(
////                    ", ",
////                    commonColumns.Select(
////                        c =>
////                            $"{sourceAlias}.[{c}]"));

////            // ФИКС №1: SET IDENTITY_INSERT ON/OFF вокруг INSERT,
////            // если у таблицы есть IDENTITY-столбец. Без этого
////            // вставка явных значений PK (перенесённых из Access)
////            // падает с ошибкой SQL 544.

////            string identityOn =
////                hasIdentity
////                    ? $"SET IDENTITY_INSERT [dbo].[{tableName}] ON;"
////                    : String.Empty;

////            string identityOff =
////                hasIdentity
////                    ? $"SET IDENTITY_INSERT [dbo].[{tableName}] OFF;"
////                    : String.Empty;

////            string sql =
////                $@"
////{identityOn}

////INSERT INTO [dbo].[{tableName}]
////(
////    {insertColumns}
////)
////SELECT
////    {sourceColumns}
////FROM {tempTable} {sourceAlias}
////WHERE NOT EXISTS
////(
////    SELECT 1
////    FROM [dbo].[{tableName}] {targetAlias}
////    WHERE {joinCondition}
////);

////{identityOff}
////";

////            Log(
////                "  Выполняем INSERT отсутствующих PK...");

////            Log(
////                $"  {sql}");

////            using (var command =
////                new SqlCommand(
////                    sql,
////                    connection))
////            {
////                command.CommandTimeout =
////                    0;

////                int inserted =
////                    command.ExecuteNonQuery();

////                // ExecuteNonQuery суммирует затронутые строки по
////                // всем инструкциям в батче (SET IDENTITY_INSERT
////                // строки затронутых не дают), так что значение
////                // остаётся корректным количеством вставленных строк.

////                Log(
////                    $"  INSERT завершен. Добавлено: {inserted}");

////                return inserted;
////            }
////        }


////        // ============================================================
////        // SALVAGE PASS: SKIP ORPHANED FK ROWS (ФИКС №4)
////        // ============================================================

////        /// <summary>
////        /// Переносит таблицу, которая падает по FOREIGN KEY даже после
////        /// всех обычных проходов. Определяет все внешние ключи
////        /// таблицы, исключает из вставки строки, чьи значения FK не
////        /// найдены в таблице, на которую они ссылаются ("сироты"), и
////        /// логирует, сколько строк и с какими значениями PK было
////        /// пропущено, чтобы это можно было разобрать вручную.
////        /// </summary>
////        private void CopyTableSkippingOrphans(
////            string tableName)
////        {
////            Log("");
////            Log(
////                $"Таблица (спасательный проход): {tableName}");

////            List<string> accessColumns =
////                GetAccessColumns(
////                    tableName);

////            List<string> sqlColumns =
////                GetSqlColumns(
////                    tableName);

////            List<string> commonColumns =
////                accessColumns
////                    .Intersect(
////                        sqlColumns,
////                        StringComparer.OrdinalIgnoreCase)
////                    .ToList();

////            if (commonColumns.Count == 0)
////            {
////                Log(
////                    "  Общих полей нет. Таблица пропущена.");

////                return;
////            }

////            List<string> pkColumns;

////            bool hasIdentity;

////            List<(string Column, string RefTable, string RefColumn)> foreignKeys;

////            using (var connection =
////                new SqlConnection(
////                    _sqlConnectionString))
////            {
////                connection.Open();

////                pkColumns =
////                    GetPrimaryKeyColumns(
////                        connection,
////                        tableName);

////                hasIdentity =
////                    HasIdentityColumn(
////                        connection,
////                        tableName);

////                foreignKeys =
////                    GetForeignKeys(
////                        connection,
////                        tableName);
////            }

////            if (pkColumns.Count == 0)
////            {
////                Log(
////                    "  PRIMARY KEY отсутствует, спасательный " +
////                    "проход неприменим. Таблица пропущена.");

////                return;
////            }

////            // Учитываем только те FK, чей столбец реально
////            // переносится (есть в commonColumns).

////            foreignKeys =
////                foreignKeys
////                    .Where(
////                        fk =>
////                            commonColumns.Contains(
////                                fk.Column,
////                                StringComparer.OrdinalIgnoreCase))
////                    .ToList();

////            if (foreignKeys.Count == 0)
////            {
////                Log(
////                    "  Внешних ключей не найдено, " +
////                    "спасательный проход неприменим.");

////                return;
////            }

////            foreach (var fk in foreignKeys)
////            {
////                Log(
////                    $"  FK: [{fk.Column}] -> " +
////                    $"[{fk.RefTable}].[{fk.RefColumn}]");
////            }

////            string tempTable =
////                BuildTempTableName(
////                    tableName) +
////                "_Salvage";

////            using (var sqlConnection =
////                new SqlConnection(
////                    _sqlConnectionString))
////            {
////                sqlConnection.Open();

////                try
////                {
////                    CreateTempTable(
////                        sqlConnection,
////                        tableName,
////                        tempTable);

////                    LoadAccessIntoTempTable(
////                        sqlConnection,
////                        tableName,
////                        tempTable,
////                        commonColumns);

////                    InsertValidRowsSkippingOrphans(
////                        sqlConnection,
////                        tableName,
////                        tempTable,
////                        commonColumns,
////                        pkColumns,
////                        foreignKeys,
////                        hasIdentity);
////                }
////                finally
////                {
////                    DropTempTable(
////                        sqlConnection,
////                        tempTable);
////                }
////            }
////        }


////        private void InsertValidRowsSkippingOrphans(
////            SqlConnection connection,
////            string tableName,
////            string tempTable,
////            List<string> commonColumns,
////            List<string> pkColumns,
////            List<(string Column, string RefTable, string RefColumn)> foreignKeys,
////            bool hasIdentity)
////        {
////            string sourceAlias =
////                "src";

////            string targetAlias =
////                "dst";

////            string dupJoinCondition =
////                String.Join(
////                    " AND ",
////                    pkColumns.Select(
////                        pk =>
////                            $"{targetAlias}.[{pk}] = " +
////                            $"{sourceAlias}.[{pk}]"));

////            // Условие "строка не сирота": для каждого FK либо
////            // значение NULL, либо оно есть в таблице, на которую
////            // ссылается FK.

////            string orphanCondition =
////                String.Join(
////                    " AND ",
////                    foreignKeys.Select(
////                        fk =>
////                            $"(" +
////                            $"{sourceAlias}.[{fk.Column}] IS NULL " +
////                            $"OR EXISTS (" +
////                            $"SELECT 1 FROM [dbo].[{fk.RefTable}] rf " +
////                            $"WHERE rf.[{fk.RefColumn}] = " +
////                            $"{sourceAlias}.[{fk.Column}]" +
////                            $")" +
////                            $")"));

////            string pkList =
////                String.Join(
////                    " + '|' + ",
////                    pkColumns.Select(
////                        pk =>
////                            $"CAST({sourceAlias}.[{pk}] AS NVARCHAR(100))"));

////            // Сначала считаем и логируем сирот — до вставки, чтобы
////            // они попали в лог, даже если что-то дальше пойдёт не так.

////            string orphanCountSql =
////                $@"
////SELECT COUNT(*)
////FROM {tempTable} {sourceAlias}
////WHERE NOT ({orphanCondition});
////";

////            using (var command =
////                new SqlCommand(
////                    orphanCountSql,
////                    connection))
////            {
////                command.CommandTimeout =
////                    0;

////                int orphanCount =
////                    Convert.ToInt32(
////                        command.ExecuteScalar());

////                if (orphanCount > 0)
////                {
////                    Log(
////                        $"  Строк-сирот (недостающий FK): {orphanCount}. " +
////                        $"Они НЕ будут перенесены.");

////                    // Разбивка по каждому FK отдельно — чтобы
////                    // сразу было видно, КАКОЙ именно внешний ключ
////                    // бьёт и сколько строк из-за него.

////                    Log(
////                        "  Разбивка сирот по конкретным FK:");

////                    foreach (var fk in foreignKeys)
////                    {
////                        string perFkCountSql =
////                            $@"
////SELECT COUNT(*)
////FROM {tempTable} {sourceAlias}
////WHERE {sourceAlias}.[{fk.Column}] IS NOT NULL
////AND NOT EXISTS
////(
////    SELECT 1
////    FROM [dbo].[{fk.RefTable}] rf
////    WHERE rf.[{fk.RefColumn}] = {sourceAlias}.[{fk.Column}]
////);
////";

////                        using (var perFkCommand =
////                            new SqlCommand(
////                                perFkCountSql,
////                                connection))
////                        {
////                            perFkCommand.CommandTimeout =
////                                0;

////                            int perFkCount =
////                                Convert.ToInt32(
////                                    perFkCommand.ExecuteScalar());

////                            if (perFkCount > 0)
////                            {
////                                Log(
////                                    $"    [{fk.Column}] -> " +
////                                    $"[{fk.RefTable}].[{fk.RefColumn}]: " +
////                                    $"{perFkCount} строк(и)");
////                            }
////                        }
////                    }

////                    string orphanSampleSql =
////                        $@"
////SELECT TOP 20
////    {pkList} AS PkValues
////FROM {tempTable} {sourceAlias}
////WHERE NOT ({orphanCondition});
////";

////                    using (var sampleCommand =
////                        new SqlCommand(
////                            orphanSampleSql,
////                            connection))
////                    {
////                        sampleCommand.CommandTimeout =
////                            0;

////                        using (var reader =
////                            sampleCommand.ExecuteReader())
////                        {
////                            Log(
////                                "  Пример PK пропущенных строк " +
////                                "(до 20 из них, формат " +
////                                String.Join(
////                                    "|",
////                                    pkColumns) +
////                                "):");

////                            while (reader.Read())
////                            {
////                                Log(
////                                    $"    - {reader.GetString(0)}");
////                            }
////                        }
////                    }
////                }
////                else
////                {
////                    Log(
////                        "  Строк-сирот не найдено (странно, " +
////                        "но продолжаем).");
////                }
////            }

////            string insertColumns =
////                String.Join(
////                    ", ",
////                    commonColumns.Select(
////                        c =>
////                            $"[{c}]"));

////            string sourceColumns =
////                String.Join(
////                    ", ",
////                    commonColumns.Select(
////                        c =>
////                            $"{sourceAlias}.[{c}]"));

////            string identityOn =
////                hasIdentity
////                    ? $"SET IDENTITY_INSERT [dbo].[{tableName}] ON;"
////                    : String.Empty;

////            string identityOff =
////                hasIdentity
////                    ? $"SET IDENTITY_INSERT [dbo].[{tableName}] OFF;"
////                    : String.Empty;

////            string insertSql =
////                $@"
////{identityOn}

////INSERT INTO [dbo].[{tableName}]
////(
////    {insertColumns}
////)
////SELECT
////    {sourceColumns}
////FROM {tempTable} {sourceAlias}
////WHERE NOT EXISTS
////(
////    SELECT 1
////    FROM [dbo].[{tableName}] {targetAlias}
////    WHERE {dupJoinCondition}
////)
////AND ({orphanCondition});

////{identityOff}
////";

////            Log(
////                "  Выполняем INSERT валидных строк " +
////                "(сироты исключены)...");

////            using (var command =
////                new SqlCommand(
////                    insertSql,
////                    connection))
////            {
////                command.CommandTimeout =
////                    0;

////                int inserted =
////                    command.ExecuteNonQuery();

////                Log(
////                    $"  INSERT завершен. Добавлено: {inserted}");
////            }
////        }


////        private List<(string Column, string RefTable, string RefColumn)> GetForeignKeys(
////            SqlConnection connection,
////            string tableName)
////        {
////            const string sql =
////@"
////SELECT
////    kcu1.COLUMN_NAME  AS FkColumn,
////    kcu2.TABLE_NAME   AS RefTable,
////    kcu2.COLUMN_NAME  AS RefColumn
////FROM
////    INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
////INNER JOIN
////    INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu1
////        ON kcu1.CONSTRAINT_NAME =
////           rc.CONSTRAINT_NAME
////INNER JOIN
////    INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu2
////        ON kcu2.CONSTRAINT_NAME =
////           rc.UNIQUE_CONSTRAINT_NAME
////       AND kcu2.ORDINAL_POSITION =
////           kcu1.ORDINAL_POSITION
////WHERE
////    kcu1.TABLE_SCHEMA = 'dbo'
////    AND kcu1.TABLE_NAME = @TableName
////ORDER BY
////    kcu1.ORDINAL_POSITION;
////";

////            var result =
////                new List<(string, string, string)>();

////            using (var command =
////                new SqlCommand(
////                    sql,
////                    connection))
////            {
////                command.CommandTimeout =
////                    0;

////                command.Parameters.Add(
////                    "@TableName",
////                    SqlDbType.NVarChar,
////                    128).Value =
////                    tableName;

////                using (var reader =
////                    command.ExecuteReader())
////                {
////                    while (reader.Read())
////                    {
////                        result.Add(
////                            (
////                                reader.GetString(0),
////                                reader.GetString(1),
////                                reader.GetString(2)
////                            ));
////                    }
////                }
////            }

////            return result;
////        }


////        // ============================================================
////        // PRIMARY KEY
////        // ============================================================

////        private List<string> GetPrimaryKeyColumns(
////            SqlConnection connection,
////            string tableName)
////        {
////            const string sql =
////@"
////SELECT
////    c.COLUMN_NAME
////FROM
////    INFORMATION_SCHEMA.KEY_COLUMN_USAGE c
////INNER JOIN
////    INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
////        ON tc.CONSTRAINT_NAME =
////           c.CONSTRAINT_NAME
////       AND tc.TABLE_NAME =
////           c.TABLE_NAME
////       AND tc.TABLE_SCHEMA =
////           c.TABLE_SCHEMA
////WHERE
////    tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
////    AND c.TABLE_NAME = @TableName
////    AND c.TABLE_SCHEMA = 'dbo'
////ORDER BY
////    c.ORDINAL_POSITION;
////";

////            using (var command =
////                new SqlCommand(
////                    sql,
////                    connection))
////            {
////                command.CommandTimeout =
////                    0;

////                command.Parameters.Add(
////                    "@TableName",
////                    SqlDbType.NVarChar,
////                    128).Value =
////                    tableName;

////                var result =
////                    new List<string>();

////                using (var reader =
////                    command.ExecuteReader())
////                {
////                    while (reader.Read())
////                    {
////                        result.Add(
////                            reader.GetString(0));
////                    }
////                }

////                return result;
////            }
////        }


////        // ============================================================
////        // COLUMNS
////        // ============================================================

////        private List<string> GetSqlColumns(
////            string tableName)
////        {
////            var result =
////                new List<string>();

////            using (var connection =
////                new SqlConnection(
////                    _sqlConnectionString))
////            {
////                connection.Open();

////                const string sql =
////@"
////SELECT
////    COLUMN_NAME
////FROM
////    INFORMATION_SCHEMA.COLUMNS
////WHERE
////    TABLE_SCHEMA = 'dbo'
////    AND TABLE_NAME = @TableName
////ORDER BY
////    ORDINAL_POSITION;
////";

////                using (var command =
////                    new SqlCommand(
////                        sql,
////                        connection))
////                {
////                    command.Parameters.Add(
////                        "@TableName",
////                        SqlDbType.NVarChar,
////                        128).Value =
////                        tableName;

////                    using (var reader =
////                        command.ExecuteReader())
////                    {
////                        while (reader.Read())
////                        {
////                            result.Add(
////                                reader.GetString(0));
////                        }
////                    }
////                }
////            }

////            return result;
////        }


////        private List<string> GetAccessColumns(
////            string tableName)
////        {
////            var result =
////                new List<string>();

////            using (var connection =
////                new OleDbConnection(
////                    GetAccessConnectionString()))
////            {
////                connection.Open();

////                using (var command =
////                    new OleDbCommand(
////                        $"SELECT * FROM [{tableName}] WHERE 1 = 0",
////                        connection))
////                using (var reader =
////                    command.ExecuteReader())
////                {
////                    DataTable schema =
////                        reader.GetSchemaTable();

////                    if (schema == null)
////                    {
////                        return result;
////                    }

////                    foreach (DataRow row in schema.Rows)
////                    {
////                        string columnName =
////                            row["ColumnName"]
////                                .ToString();

////                        if (!result.Contains(
////                            columnName,
////                            StringComparer.OrdinalIgnoreCase))
////                        {
////                            result.Add(
////                                columnName);
////                        }
////                    }
////                }
////            }

////            return result;
////        }


////        // ============================================================
////        // ACCESS TABLES
////        // ============================================================

////        private List<string> GetAccessTables()
////        {
////            var result =
////                new List<string>();

////            using (var connection =
////                new OleDbConnection(
////                    GetAccessConnectionString()))
////            {
////                connection.Open();

////                DataTable schema =
////                    connection.GetOleDbSchemaTable(
////                        OleDbSchemaGuid.Tables,
////                        new object[]
////                        {
////                            null,
////                            null,
////                            null,
////                            "TABLE"
////                        });

////                if (schema == null)
////                {
////                    return result;
////                }

////                foreach (DataRow row in schema.Rows)
////                {
////                    string tableName =
////                        row["TABLE_NAME"]
////                            .ToString();

////                    if (String.IsNullOrWhiteSpace(
////                        tableName))
////                    {
////                        continue;
////                    }

////                    if (tableName.StartsWith(
////                        "MSys",
////                        StringComparison.OrdinalIgnoreCase))
////                    {
////                        continue;
////                    }

////                    if (!result.Contains(
////                        tableName,
////                        StringComparer.OrdinalIgnoreCase))
////                    {
////                        result.Add(
////                            tableName);
////                    }
////                }
////            }

////            result.Sort(
////                StringComparer.OrdinalIgnoreCase);

////            return result;
////        }


////        // ============================================================
////        // IDENTITY
////        // ============================================================

////        private bool HasIdentityColumn(
////            SqlConnection connection,
////            string tableName)
////        {
////            const string sql =
////@"
////SELECT COUNT(*)
////FROM sys.identity_columns
////WHERE object_id =
////      OBJECT_ID(
////          N'dbo.' + @TableName,
////          N'U');
////";

////            using (var command =
////                new SqlCommand(
////                    sql,
////                    connection))
////            {
////                command.Parameters.Add(
////                    "@TableName",
////                    SqlDbType.NVarChar,
////                    128).Value =
////                    tableName;

////                return
////                    Convert.ToInt32(
////                        command.ExecuteScalar()) > 0;
////            }
////        }


////        // ============================================================
////        // HELPERS
////        // ============================================================

////        private string BuildColumnList(
////            IEnumerable<string> columns)
////        {
////            return
////                String.Join(
////                    ", ",
////                    columns.Select(
////                        c =>
////                            $"[{c}]"));
////        }


////        private string GetAccessConnectionString()
////        {
////            return
////                $"Provider=Microsoft.Jet.OLEDB.4.0;" +
////                $"Data Source={_config.MdbPath};";
////        }


////        // ============================================================
////        // LOG
////        // ============================================================

////        private void InitializeLog()
////        {
////            string logsFolder =
////                Path.Combine(
////                    AppDomain.CurrentDomain.BaseDirectory,
////                    "logs");

////            Directory.CreateDirectory(
////                logsFolder);

////            _logFilePath =
////                Path.Combine(
////                    logsFolder,
////                    $"Migration_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
////        }


////        private void Log(
////            string text)
////        {
////            string line =
////                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {text}";

////            System.Diagnostics.Debug.WriteLine(
////                line);

////            lock (_logLock)
////            {
////                File.AppendAllText(
////                    _logFilePath,
////                    line +
////                    Environment.NewLine);
////            }
////        }


////        // ============================================================
////        // SAFE DATA READER: GUID -> STRING, INVALID DATES -> NULL
////        // (ФИКС №3 и №5)
////        // ============================================================

////        /// <summary>
////        /// Оборачивает IDataReader от Access и "лечит" два вида
////        /// значений, которые SqlBulkCopy не может записать как есть:
////        ///
////        /// 1. Guid -> string (в верхнем регистре), если столбец
////        ///    назначения на самом деле varchar/nvarchar, а не
////        ///    uniqueidentifier.
////        ///
////        /// 2. DateTime вне диапазона, который понимает SQL Server
////        ///    тип datetime (1/1/1753 .. 12/31/9999) -> NULL. Access
////        ///    такие даты хранить умеет (например, дефолтные/битые
////        ///    значения вроде 0001-01-01), а SqlBulkCopy на них падает
////        ///    с SqlTypeException и валит перенос всей таблицы.
////        ///    Каждая такая замена считается по столбцам и после
////        ///    переноса таблицы выводится в лог методом LogAnomalies,
////        ///    чтобы аномалию можно было найти и разобрать вручную.
////        ///
////        /// Все остальные методы просто делегируются исходному reader.
////        /// </summary>
////        private sealed class AccessSafeDataReader : IDataReader
////        {
////            private static readonly DateTime SqlDateTimeMin =
////                new DateTime(1753, 1, 1);

////            private static readonly DateTime SqlDateTimeMax =
////                new DateTime(
////                    9999, 12, 31, 23, 59, 59, 997);

////            private readonly IDataReader _inner;

////            private readonly Action<string> _log;

////            private readonly HashSet<int> _guidColumns;

////            private readonly Dictionary<int, int> _invalidDateCounts =
////                new Dictionary<int, int>();

////            public AccessSafeDataReader(
////                IDataReader inner,
////                Action<string> log)
////            {
////                _inner =
////                    inner ??
////                    throw new ArgumentNullException(
////                        nameof(inner));

////                _log =
////                    log ??
////                    throw new ArgumentNullException(
////                        nameof(log));

////                _guidColumns =
////                    new HashSet<int>();

////                for (int i = 0; i < _inner.FieldCount; i++)
////                {
////                    if (_inner.GetFieldType(i) == typeof(Guid))
////                    {
////                        _guidColumns.Add(i);
////                    }
////                }
////            }

////            private object Sanitize(
////                int i,
////                object value)
////            {
////                if (_guidColumns.Contains(i) &&
////                    value is Guid guid)
////                {
////                    return
////                        guid.ToString()
////                            .ToUpperInvariant();
////                }

////                if (value is DateTime dateTime &&
////                    (dateTime < SqlDateTimeMin ||
////                     dateTime > SqlDateTimeMax))
////                {
////                    if (!_invalidDateCounts.ContainsKey(i))
////                    {
////                        _invalidDateCounts[i] =
////                            0;
////                    }

////                    _invalidDateCounts[i]++;

////                    return DBNull.Value;
////                }

////                return value;
////            }

////            /// <summary>
////            /// Логирует, сколько невалидных дат было заменено на
////            /// NULL, в разбивке по столбцам. Вызывать после
////            /// WriteToServer, пока reader ещё не Dispose().
////            /// </summary>
////            public void LogAnomalies(
////                string tableName)
////            {
////                foreach (var kvp in _invalidDateCounts)
////                {
////                    string columnName =
////                        _inner.GetName(
////                            kvp.Key);

////                    _log(
////                        $"  ВНИМАНИЕ: столбец [{columnName}] таблицы " +
////                        $"{tableName} — {kvp.Value} значений даты вне " +
////                        $"диапазона SQL Server (1753-9999). " +
////                        $"Заменены на NULL.");
////                }
////            }

////            public object GetValue(
////                int i)
////            {
////                return
////                    Sanitize(
////                        i,
////                        _inner.GetValue(i));
////            }

////            public int GetValues(
////                object[] values)
////            {
////                int count =
////                    _inner.GetValues(
////                        values);

////                for (int i = 0; i < count; i++)
////                {
////                    values[i] =
////                        Sanitize(
////                            i,
////                            values[i]);
////                }

////                return count;
////            }

////            public Type GetFieldType(
////                int i)
////            {
////                if (_guidColumns.Contains(i))
////                {
////                    return typeof(string);
////                }

////                return
////                    _inner.GetFieldType(i);
////            }

////            public string GetDataTypeName(
////                int i)
////            {
////                if (_guidColumns.Contains(i))
////                {
////                    return "string";
////                }

////                return
////                    _inner.GetDataTypeName(i);
////            }

////            public string GetString(
////                int i)
////            {
////                if (_guidColumns.Contains(i))
////                {
////                    object value =
////                        _inner.GetValue(i);

////                    if (value is Guid guid)
////                    {
////                        return
////                            guid.ToString()
////                                .ToUpperInvariant();
////                    }

////                    return
////                        value?.ToString();
////                }

////                return
////                    _inner.GetString(i);
////            }

////            // ---- всё остальное — прямое делегирование ----

////            public int FieldCount =>
////                _inner.FieldCount;

////            public int Depth =>
////                _inner.Depth;

////            public bool IsClosed =>
////                _inner.IsClosed;

////            public int RecordsAffected =>
////                _inner.RecordsAffected;

////            public object this[int i] =>
////                GetValue(i);

////            public object this[string name] =>
////                GetValue(
////                    _inner.GetOrdinal(name));

////            public void Close() =>
////                _inner.Close();

////            public void Dispose() =>
////                _inner.Dispose();

////            public bool NextResult() =>
////                _inner.NextResult();

////            public bool Read() =>
////                _inner.Read();

////            public DataTable GetSchemaTable() =>
////                _inner.GetSchemaTable();

////            public bool GetBoolean(int i) =>
////                _inner.GetBoolean(i);

////            public byte GetByte(int i) =>
////                _inner.GetByte(i);

////            public long GetBytes(
////                int i,
////                long fieldOffset,
////                byte[] buffer,
////                int bufferoffset,
////                int length) =>
////                _inner.GetBytes(
////                    i,
////                    fieldOffset,
////                    buffer,
////                    bufferoffset,
////                    length);

////            public char GetChar(int i) =>
////                _inner.GetChar(i);

////            public long GetChars(
////                int i,
////                long fieldoffset,
////                char[] buffer,
////                int bufferoffset,
////                int length) =>
////                _inner.GetChars(
////                    i,
////                    fieldoffset,
////                    buffer,
////                    bufferoffset,
////                    length);

////            public IDataReader GetData(int i) =>
////                _inner.GetData(i);

////            public Guid GetGuid(int i) =>
////                _inner.GetGuid(i);

////            public DateTime GetDateTime(int i) =>
////                _inner.GetDateTime(i);

////            public decimal GetDecimal(int i) =>
////                _inner.GetDecimal(i);

////            public double GetDouble(int i) =>
////                _inner.GetDouble(i);

////            public float GetFloat(int i) =>
////                _inner.GetFloat(i);

////            public short GetInt16(int i) =>
////                _inner.GetInt16(i);

////            public int GetInt32(int i) =>
////                _inner.GetInt32(i);

////            public long GetInt64(int i) =>
////                _inner.GetInt64(i);

////            public string GetName(int i) =>
////                _inner.GetName(i);

////            public int GetOrdinal(string name) =>
////                _inner.GetOrdinal(name);

////            public bool IsDBNull(int i) =>
////                _inner.IsDBNull(i);
////        }
////    }
////}