using RRStoSQL.Services.AccessToSql.Models;
using System;
using System.Collections.Generic;

public class AccessDatabaseInfo
{
    public string FileName { get; set; }

    public List<AccessTableInfo> Tables { get; }
        = new List<AccessTableInfo>();

    /// <summary>
    /// Быстрый поиск таблицы по имени.
    /// Используется при чтении схемы Access.
    /// </summary>
    internal Dictionary<string, AccessTableInfo> TableMap { get; }
        = new Dictionary<string, AccessTableInfo>(
            StringComparer.OrdinalIgnoreCase);
}