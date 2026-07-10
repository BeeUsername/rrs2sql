using System.Data.OleDb;

namespace RRStoSQL.Services.AccessToSql.Models
{
    /// <summary>
    /// Описание одного поля Access.
    /// Используется при создании SQL Server таблицы.
    /// </summary>
    public class AccessColumnInfo
    {
        /// <summary>
        /// Имя поля.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Тип данных Access.
        /// Например:
        /// Integer,
        /// VarChar,
        /// LongChar,
        /// Date.
        /// </summary>
        public OleDbType AccessType
        {
            get;
            set;
        }

        /// <summary>
        /// Максимальная длина.
        /// Для TEXT.
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Разрешены ли NULL.
        /// </summary>
        public bool AllowNull { get; set; }

        /// <summary>
        /// Поле AutoNumber.
        /// </summary>
        public bool IsIdentity { get; set; }
    }
}