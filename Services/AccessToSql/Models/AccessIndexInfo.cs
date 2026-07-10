using System.Collections.Generic;

namespace RRStoSQL.Services.AccessToSql.Models
{
    /// <summary>
    /// Описание индекса Access.
    /// </summary>
    public class AccessIndexInfo
    {
        /// <summary>
        /// Имя индекса.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Поля индекса.
        /// </summary>
        public List<string> Columns { get; }
            = new List<string>();

        /// <summary>
        /// Уникальный индекс.
        /// </summary>
        public bool IsUnique { get; set; }
    }
}