namespace RRStoSQL.Services.AccessToSql.Models
{
    /// <summary>
    /// Описание внешнего ключа Access.
    /// </summary>
    public class AccessForeignKeyInfo
    {
        /// <summary>
        /// Имя ограничения.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Таблица-источник.
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// Поле-источник.
        /// </summary>
        public string ColumnName { get; set; }

        /// <summary>
        /// Родительская таблица.
        /// </summary>
        public string ParentTable { get; set; }

        /// <summary>
        /// Родительское поле.
        /// </summary>
        public string ParentColumn { get; set; }
    }
}