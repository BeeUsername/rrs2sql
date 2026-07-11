// MdbToSqlMigrator.cs
//
// Требования к окружению:
//   - Windows (Jet OLEDB 4.0 предустановлен с Windows 2000+, ничего ставить не нужно)
//   - Сборка проекта под платформу x86 (Jet OLEDB провайдер только 32-битный)
//   - ADOX (msadox.dll) — используется через позднее связывание (COM), регистрируется
//     на машине вместе с Jet OLEDB как часть тех же компонентов Windows Data Access
//     Components. Если Jet OLEDB у вас уже работает, ADOX почти наверняка тоже
//     присутствует. Используется для чтения Default Value, Description,
//     Validation Rule/Text и Cascade Delete/Update — эти данные ненадёжно или вообще
//     не читаются через стандартные OleDbSchemaGuid.
//
// NuGet: Microsoft.Data.SqlClient (или System.Data.SqlClient)

using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;

class MdbToSqlMigrator
{
    private readonly string _mdbPath;
    private readonly string _sqlConnStr;
    private readonly Action<string> _log;

    public MdbToSqlMigrator(string mdbPath, string sqlConnStr, Action<string> progress = null)
    {
        _mdbPath = mdbPath;
        _sqlConnStr = sqlConnStr;
        _log = progress ?? (msg => Console.WriteLine(msg));
    }

    private string MdbConnStr => $@"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={_mdbPath};";

    class ColumnInfo
    {
        public string Name;
        public Type NetType;
        public int ProviderType;      // OleDbType (числовой код из схемы MDB)
        public int? Size;
        public int? Precision;
        public int? Scale;
        public string SqlType;
        public bool IsAutoIncrement;
        public bool AllowDBNull;
        public string DefaultValueRaw;   // как хранится в Access
        public string DefaultValueSql;   // переведено в T-SQL (или null, если не смогли)
        public string Description;
        public string ValidationRuleRaw;
        public string ValidationRuleSql; // CHECK-выражение (или null)
        public string ValidationText;
    }

    // ==================== ГЛАВНЫЙ МЕТОД ====================

    public void MigrateAll()
    {
        var tables = GetTableNames();
        var tableColumns = new Dictionary<string, List<ColumnInfo>>();
        int total = tables.Count;
        int idx = 0;

        // ADOX-каталог открываем один раз на всю миграцию (дорогая операция)
        dynamic adoxCatalog = OpenAdoxCatalog(out dynamic adoxConn);

        try
        {
            // 1. Схема + метаданные (default/description/validation) + создание таблиц
            foreach (var table in tables)
            {
                idx++;
                _log($"[{idx}/{total}] Схема: {table}");
                var cols = GetColumns(table);
                EnrichWithAdoxMetadata(adoxCatalog, table, cols);
                tableColumns[table] = cols;
                CreateSqlTable(table, cols);
            }

            // 2. Данные (с сохранением identity)
            idx = 0;
            foreach (var table in tables)
            {
                idx++;
                _log($"[{idx}/{total}] Данные: {table}...");
                long rows = CopyData(table, tableColumns[table]);
                _log($"[{idx}/{total}] Данные: {table} — {rows} строк");
            }

            // 3. Reseed identity — критично, иначе следующая вставка после
            //    миграции начнёт нумерацию не с того места, где Access остановился
            idx = 0;
            foreach (var table in tables)
            {
                idx++;
                var idCol = tableColumns[table].FirstOrDefault(c => c.IsAutoIncrement);
                if (idCol != null)
                {
                    _log($"[{idx}/{total}] Reseed identity: {table}.{idCol.Name}");
                    ReseedIdentity(table, idCol.Name);
                }
            }

            // 4. Индексы (кроме PK — он уже создан вместе с таблицей)
            idx = 0;
            foreach (var table in tables)
            {
                idx++;
                _log($"[{idx}/{total}] Индексы: {table}");
                CreateIndexes(table);
            }

            // 5. Default Values
            idx = 0;
            foreach (var table in tables)
            {
                idx++;
                ApplyDefaultValues(table, tableColumns[table]);
            }
            _log("Default values применены (там, где удалось перевести выражение).");

            // 6. Validation Rules -> CHECK
            idx = 0;
            foreach (var table in tables)
            {
                idx++;
                ApplyValidationRules(table, tableColumns[table]);
            }
            _log("Validation rules применены (простые случаи).");

            // 7. Descriptions -> Extended Properties
            idx = 0;
            foreach (var table in tables)
            {
                idx++;
                ApplyDescriptions(table, tableColumns[table]);
            }
            _log("Descriptions перенесены в extended properties.");

            // 8. Внешние ключи (в самом конце, с учётом Cascade Delete/Update)
            _log("Внешние ключи (с учётом Cascade Delete/Update)...");
            CreateForeignKeysViaAdox(adoxCatalog);

            _log("Миграция завершена.");
        }
        finally
        {
            CloseAdoxCatalog(adoxConn);
        }
    }

    // ==================== СХЕМА ТАБЛИЦ ====================

    List<string> GetTableNames()
    {
        var tables = new List<string>();
        using var conn = new OleDbConnection(MdbConnStr);
        conn.Open();

        var schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables,
            new object[] { null, null, null, "TABLE" });

        foreach (DataRow row in schema.Rows)
        {
            var name = row["TABLE_NAME"].ToString();
            if (!name.StartsWith("MSys"))
                tables.Add(name);
        }
        return tables;
    }

    List<ColumnInfo> GetColumns(string tableName)
    {
        using var mdbConn = new OleDbConnection(MdbConnStr);
        mdbConn.Open();

        using var cmd = new OleDbCommand($"SELECT * FROM [{tableName}] WHERE 1=0", mdbConn);
        using var reader = cmd.ExecuteReader();
        var schemaTable = reader.GetSchemaTable();

        var result = new List<ColumnInfo>();
        foreach (DataRow col in schemaTable.Rows)
        {
            var type = (Type)col["DataType"];
            bool isAutoInc = col["IsAutoIncrement"] != DBNull.Value && (bool)col["IsAutoIncrement"];
            bool allowNull = col["AllowDBNull"] == DBNull.Value || (bool)col["AllowDBNull"];
            int providerType = col["ProviderType"] != DBNull.Value ? Convert.ToInt32(col["ProviderType"]) : -1;

            int? size = col["ColumnSize"] != DBNull.Value ? Convert.ToInt32(col["ColumnSize"]) : (int?)null;
            int? precision = col.Table.Columns.Contains("NumericPrecision") && col["NumericPrecision"] != DBNull.Value
                ? Convert.ToInt32(col["NumericPrecision"]) : (int?)null;
            int? scale = col.Table.Columns.Contains("NumericScale") && col["NumericScale"] != DBNull.Value
                ? Convert.ToInt32(col["NumericScale"]) : (int?)null;

            var info = new ColumnInfo
            {
                Name = col["ColumnName"].ToString(),
                NetType = type,
                ProviderType = providerType,
                Size = size,
                Precision = precision,
                Scale = scale,
                IsAutoIncrement = isAutoInc,
                AllowDBNull = allowNull
            };
            info.SqlType = MapType(info);
            result.Add(info);
        }
        return result;
    }

    // Точный маппинг по ProviderType (числовой код OleDbType из Jet-схемы),
    // с fallback на .NET Type, если код не распознан.
    string MapType(ColumnInfo c)
    {
        switch (c.ProviderType)
        {
            case 2: return "SMALLINT";                       // SmallInt
            case 3: return "INT";                             // Integer (Long Integer)
            case 4: return "REAL";                             // Single
            case 5: return "FLOAT";                            // Double
            case 6: return "MONEY";                            // Currency  <-- было DECIMAL(18,4)
            case 7: return "DATETIME";                         // Date/Time
            case 11: return "BIT";                             // Boolean (Yes/No)
            case 17: return "TINYINT";                         // UnsignedTinyInt (Byte)
            case 20: return "BIGINT";                          // BigInt
            case 72: return "UNIQUEIDENTIFIER";                // Guid (Replication ID)
            case 128: case 204: return $"VARBINARY({SizeOrMax(c.Size)})"; // Binary/VarBinary
            case 205: return "VARBINARY(MAX)";                 // LongVarBinary — OLE Object
            case 130: case 202: return $"NVARCHAR({SizeOrMax(c.Size)})"; // WChar/VarWChar — Text(N)
            case 203: return "NVARCHAR(MAX)";                  // LongVarWChar — Memo *и* Hyperlink
                                                               // (Jet-схема не различает эти два подтипа
                                                               //  через ProviderType; если нужно точное
                                                               //  различение Hyperlink, требуется отдельный
                                                               //  запрос через ADOX Field.Attributes —
                                                               //  не реализовано, см. примечание в чате)
            case 131:                                           // Numeric/Decimal
                int p = c.Precision ?? 18;
                int s = c.Scale ?? 4;
                return $"DECIMAL({p},{s})";
        }

        // fallback по .NET-типу, если ProviderType не распознан выше
        return c.NetType switch
        {
            _ when c.NetType == typeof(string) => $"NVARCHAR({SizeOrMax(c.Size)})",
            _ when c.NetType == typeof(int) => "INT",
            _ when c.NetType == typeof(short) => "SMALLINT",
            _ when c.NetType == typeof(long) => "BIGINT",
            _ when c.NetType == typeof(byte) => "TINYINT",
            _ when c.NetType == typeof(double) => "FLOAT",
            _ when c.NetType == typeof(float) => "REAL",
            _ when c.NetType == typeof(decimal) => "DECIMAL(18,4)",
            _ when c.NetType == typeof(DateTime) => "DATETIME",
            _ when c.NetType == typeof(bool) => "BIT",
            _ when c.NetType == typeof(Guid) => "UNIQUEIDENTIFIER",
            _ when c.NetType == typeof(byte[]) => "VARBINARY(MAX)",
            _ => "NVARCHAR(MAX)"
        };
    }

    string SizeOrMax(int? size)
    {
        if (size == null || size <= 0 || size > 4000) return "MAX";
        return size.ToString();
    }

    void CreateSqlTable(string tableName, List<ColumnInfo> columns)
    {
        var pkColumns = GetPrimaryKeyColumns(tableName);
        var defs = new List<string>();

        foreach (var c in columns)
        {
            // Nullability из Jet метадаты не всегда надёжна — PK всегда NOT NULL,
            // остальным даём NULL по умолчанию если сомнительно, чтобы не падал BulkCopy
            string nullability = pkColumns.Contains(c.Name) ? "NOT NULL"
                                  : (c.AllowDBNull ? "NULL" : "NOT NULL");
            string identity = c.IsAutoIncrement ? "IDENTITY(1,1)" : "";
            defs.Add($"[{c.Name}] {c.SqlType} {identity} {nullability}".Trim());
        }

        string pkConstraint = "";
        if (pkColumns.Count > 0)
        {
            string pkCols = string.Join(", ", pkColumns.Select(c => $"[{c}]"));
            pkConstraint = $", CONSTRAINT [PK_{SafeName(tableName)}] PRIMARY KEY ({pkCols})";
        }

        string createSql =
            $"IF OBJECT_ID('{tableName}', 'U') IS NOT NULL DROP TABLE [{tableName}]; " +
            $"CREATE TABLE [{tableName}] ({string.Join(", ", defs)}{pkConstraint})";

        ExecSql(createSql);
    }

    List<string> GetPrimaryKeyColumns(string tableName)
    {
        var result = new List<string>();
        using var conn = new OleDbConnection(MdbConnStr);
        conn.Open();

        var schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Primary_Keys,
            new object[] { null, null, tableName });

        foreach (DataRow row in schema.Rows)
            result.Add(row["COLUMN_NAME"].ToString());

        return result;
    }

    // ==================== ДАННЫЕ ====================

    long CopyData(string tableName, List<ColumnInfo> columns)
    {
        using var mdbConn = new OleDbConnection(MdbConnStr);
        mdbConn.Open();

        using var cmd = new OleDbCommand($"SELECT * FROM [{tableName}]", mdbConn);
        using var reader = cmd.ExecuteReader();

        if (!reader.HasRows) return 0;

        bool hasIdentity = columns.Any(c => c.IsAutoIncrement);
        var options = SqlBulkCopyOptions.Default;
        if (hasIdentity)
            options |= SqlBulkCopyOptions.KeepIdentity;

        using var sqlConn = new SqlConnection(_sqlConnStr);
        sqlConn.Open();

        using var bulkCopy = new SqlBulkCopy(sqlConn, options, null)
        {
            DestinationTableName = tableName,
            BulkCopyTimeout = 0
        };

        foreach (var c in columns)
            bulkCopy.ColumnMappings.Add(c.Name, c.Name);

        long rowCount = 0;
        bulkCopy.NotifyAfter = 1000;
        bulkCopy.SqlRowsCopied += (s, e) => rowCount = e.RowsCopied;

        bulkCopy.WriteToServer(reader);

        // если строк меньше NotifyAfter, событие не сработает — досчитаем отдельно
        using var countConn = new OleDbConnection(MdbConnStr);
        countConn.Open();
        using var countCmd = new OleDbCommand($"SELECT COUNT(*) FROM [{tableName}]", countConn);
        return Convert.ToInt64(countCmd.ExecuteScalar());
    }

    void ReseedIdentity(string tableName, string idColumn)
    {
        // если таблица пустая — reseed не нужен (и упадёт на MAX(NULL))
        string sql = $@"
            IF EXISTS (SELECT 1 FROM [{tableName}])
                DBCC CHECKIDENT ('{tableName}', RESEED, (SELECT MAX([{idColumn}]) FROM [{tableName}]))
            ELSE
                DBCC CHECKIDENT ('{tableName}', RESEED, 0)";
        try { ExecSql(sql); }
        catch (Exception ex) { _log($"  Не удалось сделать reseed для {tableName}: {ex.Message}"); }
    }

    // ==================== ИНДЕКСЫ ====================

    void CreateIndexes(string tableName)
    {
        using var conn = new OleDbConnection(MdbConnStr);
        conn.Open();

        var schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Indexes,
            new object[] { null, null, null, null, tableName });

        var grouped = schema.Rows.Cast<DataRow>()
            .Where(r => (bool)r["PRIMARY_KEY"] == false)
            .GroupBy(r => r["INDEX_NAME"].ToString());

        foreach (var group in grouped)
        {
            var ordered = group.OrderBy(r => (short)r["ORDINAL_POSITION"]).ToList();
            bool isUnique = (bool)ordered[0]["UNIQUE"];
            string indexName = SafeName($"IX_{tableName}_{group.Key}");
            string cols = string.Join(", ", ordered.Select(r =>
            {
                string colName = r["COLUMN_NAME"].ToString();
                short collation = (short)r["COLLATION"];
                string dir = collation == 2 ? "DESC" : "ASC";
                return $"[{colName}] {dir}";
            }));

            string uniqueKw = isUnique ? "UNIQUE" : "";
            string sql = $"CREATE {uniqueKw} INDEX [{indexName}] ON [{tableName}] ({cols})";

            try { ExecSql(sql); }
            catch (Exception ex) { _log($"  Пропущен индекс {indexName}: {ex.Message}"); }
        }
    }

    // ==================== ADOX: ОТКРЫТИЕ КАТАЛОГА ====================

    dynamic OpenAdoxCatalog(out dynamic adoConnection)
    {
        try
        {
            var catalogType = Type.GetTypeFromProgID("ADOX.Catalog");
            var connType = Type.GetTypeFromProgID("ADODB.Connection");

            if (catalogType == null || connType == null)
            {
                _log("ADOX не найден на машине — Default Values / Validation Rules / " +
                     "Description / Cascade Delete-Update перенесены НЕ будут. " +
                     "Остальная миграция (схема, данные, индексы, FK без cascade) продолжится.");
                adoConnection = null;
                return null;
            }

            dynamic conn = Activator.CreateInstance(connType);
            conn.Open(MdbConnStr, "", "", 0);

            dynamic catalog = Activator.CreateInstance(catalogType);
            catalog.ActiveConnection = conn;

            adoConnection = conn;
            return catalog;
        }
        catch (Exception ex)
        {
            _log($"Не удалось открыть ADOX-каталог ({ex.Message}). " +
                 "Расширенные метаданные (default/validation/description/cascade) пропущены.");
            adoConnection = null;
            return null;
        }
    }

    void CloseAdoxCatalog(dynamic adoConnection)
    {
        try { adoConnection?.Close(); } catch { /* ignore */ }
    }

    // ==================== ADOX: DEFAULT / DESCRIPTION / VALIDATION ====================

    void EnrichWithAdoxMetadata(dynamic catalog, string tableName, List<ColumnInfo> columns)
    {
        if (catalog == null) return;

        try
        {
            dynamic table = catalog.Tables[tableName];
            foreach (var col in columns)
            {
                dynamic adoxCol;
                try { adoxCol = table.Columns[col.Name]; }
                catch { continue; }

                col.DefaultValueRaw = TryGetAdoxProperty(adoxCol, "Default");
                col.Description = TryGetAdoxProperty(adoxCol, "Description");
                col.ValidationRuleRaw = TryGetAdoxProperty(adoxCol, "Validation Rule") ??
                                         TryGetAdoxProperty(adoxCol, "Rule Validation Rule");
                col.ValidationText = TryGetAdoxProperty(adoxCol, "Validation Text") ??
                                      TryGetAdoxProperty(adoxCol, "Rule Validation Text");

                if (!string.IsNullOrWhiteSpace(col.DefaultValueRaw))
                    col.DefaultValueSql = TranslateDefaultExpression(col.DefaultValueRaw);

                if (!string.IsNullOrWhiteSpace(col.ValidationRuleRaw))
                    col.ValidationRuleSql = TranslateValidationRule(col.ValidationRuleRaw);
            }
        }
        catch (Exception ex)
        {
            _log($"  ADOX: не удалось прочитать метаданные таблицы {tableName}: {ex.Message}");
        }
    }

    string TryGetAdoxProperty(dynamic adoxColumn, string propName)
    {
        try
        {
            var val = adoxColumn.Properties[propName].Value;
            if (val == null || val is DBNull) return null;
            var s = val.ToString();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }
        catch
        {
            return null; // свойство отсутствует для этого типа колонки — это нормально
        }
    }

    // Переводит распространённые выражения Access DefaultValue в T-SQL.
    // Возвращает null, если выражение не распознано (тогда default просто не создаётся,
    // а не создаётся "как попало").
    string TranslateDefaultExpression(string raw)
    {
        string expr = raw.Trim();

        if (expr.Equals("Now()", StringComparison.OrdinalIgnoreCase)) return "GETDATE()";
        if (expr.Equals("Date()", StringComparison.OrdinalIgnoreCase)) return "CAST(GETDATE() AS DATE)";
        if (expr.Equals("True", StringComparison.OrdinalIgnoreCase) || expr == "-1") return "1";
        if (expr.Equals("False", StringComparison.OrdinalIgnoreCase) || expr == "0") return "0";

        // числовой литерал
        if (Regex.IsMatch(expr, @"^-?\d+(\.\d+)?$")) return expr;

        // строковый литерал в кавычках Access: "текст"
        var strMatch = Regex.Match(expr, "^\"(.*)\"$");
        if (strMatch.Success) return "'" + strMatch.Groups[1].Value.Replace("'", "''") + "'";

        return null; // неизвестное выражение — не рискуем угадывать
    }

    // Переводит простые Validation Rule в CHECK-выражение T-SQL.
    // Осознанно НЕ пытается парсить функции (IsDate, Like с Access-масками и т.п.) —
    // такие правила пропускаются с логом, а не транслируются "примерно".
    string TranslateValidationRule(string raw)
    {
        string expr = raw.Trim();

        // отклоняем сложные конструкции, которые не можем надёжно перевести
        if (Regex.IsMatch(expr, @"\b(Like|IsDate|IsNull|InStr|DLookup|DCount)\b", RegexOptions.IgnoreCase))
            return null;

        string result = expr;
        result = Regex.Replace(result, @"\bAnd\b", "AND", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bOr\b", "OR", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bNot\b", "NOT", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bBetween\b", "BETWEEN", RegexOptions.IgnoreCase);

        // допускаем только простые операторы сравнения / логику / диапазоны
        if (!Regex.IsMatch(result, @"^[\w\s\.\-\+\(\)'""<>=ANDORT BETWEE,]+$"))
            return null;

        return result;
    }

    void ApplyDefaultValues(string tableName, List<ColumnInfo> columns)
    {
        foreach (var c in columns.Where(c => c.DefaultValueSql != null))
        {
            string sql = $"ALTER TABLE [{tableName}] ADD CONSTRAINT [DF_{SafeName(tableName)}_{SafeName(c.Name)}] " +
                         $"DEFAULT ({c.DefaultValueSql}) FOR [{c.Name}]";
            try { ExecSql(sql); }
            catch (Exception ex) { _log($"  Пропущен default {tableName}.{c.Name}: {ex.Message}"); }
        }

        var skipped = columns.Where(c => c.DefaultValueRaw != null && c.DefaultValueSql == null).ToList();
        foreach (var c in skipped)
            _log($"  Default для {tableName}.{c.Name} не переведён автоматически: \"{c.DefaultValueRaw}\" — задайте вручную при необходимости.");
    }

    void ApplyValidationRules(string tableName, List<ColumnInfo> columns)
    {
        foreach (var c in columns.Where(c => c.ValidationRuleSql != null))
        {
            string sql = $"ALTER TABLE [{tableName}] ADD CONSTRAINT [CK_{SafeName(tableName)}_{SafeName(c.Name)}] " +
                         $"CHECK ([{c.Name}] {c.ValidationRuleSql})";
            try { ExecSql(sql); }
            catch (Exception ex) { _log($"  Пропущен CHECK {tableName}.{c.Name}: {ex.Message}"); }
        }

        var skipped = columns.Where(c => c.ValidationRuleRaw != null && c.ValidationRuleSql == null).ToList();
        foreach (var c in skipped)
            _log($"  Validation Rule для {tableName}.{c.Name} не переведена автоматически: \"{c.ValidationRuleRaw}\" — перенесите вручную, если правило важно.");
    }

    void ApplyDescriptions(string tableName, List<ColumnInfo> columns)
    {
        foreach (var c in columns.Where(c => !string.IsNullOrWhiteSpace(c.Description)))
        {
            string sql = $@"
                EXEC sys.sp_addextendedproperty
                    @name = N'MS_Description', @value = @desc,
                    @level0type = N'SCHEMA', @level0name = N'dbo',
                    @level1type = N'TABLE',  @level1name = @tbl,
                    @level2type = N'COLUMN', @level2name = @col";

            try
            {
                using var conn = new SqlConnection(_sqlConnStr);
                conn.Open();
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@desc", c.Description);
                cmd.Parameters.AddWithValue("@tbl", tableName);
                cmd.Parameters.AddWithValue("@col", c.Name);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _log($"  Пропущено description {tableName}.{c.Name}: {ex.Message}");
            }
        }
    }

    // ==================== ADOX: ВНЕШНИЕ КЛЮЧИ С CASCADE ====================

    void CreateForeignKeysViaAdox(dynamic catalog)
    {
        if (catalog == null)
        {
            _log("  ADOX недоступен — внешние ключи не созданы. " +
                 "Можно добавить их вручную или через OleDbSchemaGuid.Foreign_Keys без cascade-правил.");
            return;
        }

        try
        {
            dynamic tables = catalog.Tables;
            foreach (dynamic table in tables)
            {
                string tableName = table.Name;
                if (string.IsNullOrEmpty(tableName) || tableName.StartsWith("MSys")) continue;

                dynamic keys;
                try { keys = table.Keys; } catch { continue; }

                foreach (dynamic key in keys)
                {
                    int keyType;
                    try { keyType = (int)key.Type; } catch { continue; }

                    // adKeyForeign = 2
                    if (keyType != 2) continue;

                    string fkName = key.Name;
                    string relatedTable = key.RelatedTable;

                    var fkCols = new List<string>();
                    var pkCols = new List<string>();
                    foreach (dynamic col in key.Columns)
                    {
                        fkCols.Add((string)col.Name);
                        pkCols.Add((string)col.RelatedColumn);
                    }

                    if (fkCols.Count == 0) continue;

                    // RuleEnum: adRINone=0, adRICascade=1, adRISetNull=2, adRISetDefault=3
                    int updateRule = 0, deleteRule = 0;
                    try { updateRule = (int)key.UpdateRule; } catch { }
                    try { deleteRule = (int)key.DeleteRule; } catch { }

                    string onUpdate = updateRule == 1 ? "ON UPDATE CASCADE" : "";
                    string onDelete = deleteRule == 1 ? "ON DELETE CASCADE" : "";

                    string constraintName = SafeName($"FK_{tableName}_{relatedTable}_{fkName}");
                    string sql = $@"ALTER TABLE [{tableName}] ADD CONSTRAINT [{constraintName}]
                        FOREIGN KEY ({string.Join(", ", fkCols.Select(c => $"[{c}]"))})
                        REFERENCES [{relatedTable}] ({string.Join(", ", pkCols.Select(c => $"[{c}]"))})
                        {onDelete} {onUpdate}";

                    try
                    {
                        ExecSql(sql);
                        _log($"  FK создан: {constraintName}" +
                             (onDelete != "" || onUpdate != "" ? $" ({onDelete} {onUpdate})".Trim() : ""));
                    }
                    catch (Exception ex)
                    {
                        _log($"  Пропущен FK {constraintName}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log($"  Ошибка при чтении связей через ADOX: {ex.Message}");
        }
    }

    // ==================== ВСПОМОГАТЕЛЬНОЕ ====================

    void ExecSql(string sql)
    {
        using var conn = new SqlConnection(_sqlConnStr);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 0 };
        cmd.ExecuteNonQuery();
    }

    string SafeName(string name) =>
        Regex.Replace(name, @"[^\w]", "_");
}