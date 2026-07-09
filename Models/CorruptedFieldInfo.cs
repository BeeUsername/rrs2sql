namespace RRStoSQL.Models
{
    /// <summary>
    /// Информация о найденном
    /// поврежденном поле.
    /// </summary>
    public class CorruptedFieldInfo
    {
        /// <summary>
        /// Имя таблицы.
        /// </summary>
        public string TableName
        {
            get;
            set;
        }

        /// <summary>
        /// Имя поля.
        /// </summary>
        public string FieldName
        {
            get;
            set;
        }

        /// <summary>
        /// Найденное значение.
        /// </summary>
        public string Value
        {
            get;
            set;
        }
    }
}