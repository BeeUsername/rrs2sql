using System;
using System.Collections.Generic;

namespace RRStoSQL.Services.AccessToSql.Models
{
    /// <summary>
    /// Полное описание таблицы Access.
    /// </summary>
    public class AccessTableInfo
    {
        /// <summary>
        /// Имя таблицы.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Колонки таблицы.
        /// </summary>
        public List<AccessColumnInfo> Columns { get; }
            = new List<AccessColumnInfo>();

        /// <summary>
        /// Быстрый поиск колонки по имени.
        /// Используется при чтении PK/FK/индексов.
        /// </summary>
        internal Dictionary<string, AccessColumnInfo> ColumnMap { get; }
            = new Dictionary<string, AccessColumnInfo>(
                StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Поля первичного ключа.
        /// </summary>
        public List<string> PrimaryKeyColumns { get; }
            = new List<string>();

        /// <summary>
        /// Индексы.
        /// </summary>
        public List<AccessIndexInfo> Indexes { get; }
            = new List<AccessIndexInfo>();

        /// <summary>
        /// Внешние ключи.
        /// </summary>
        public List<AccessForeignKeyInfo> ForeignKeys { get; }
            = new List<AccessForeignKeyInfo>();
    }
}