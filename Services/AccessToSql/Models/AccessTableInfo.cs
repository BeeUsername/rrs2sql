using System.Collections.Generic;

namespace RRStoSQL.Services.AccessToSql.Models
{
    /// <summary>
    /// Полное описание таблицы Access.
    /// Используется для построения структуры SQL Server.
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
        /// Первичный ключ.
        /// Обычно один, но может быть составным.
        /// </summary>
        public List<string> PrimaryKeyColumns { get; }
            = new List<string>();

        /// <summary>
        /// Индексы таблицы.
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