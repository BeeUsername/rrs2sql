namespace RRStoSQL.Models
{
    /// <summary>
    /// Описание таблицы,
    /// подлежащей проверке.
    /// </summary>
    public class TableCheckInfo
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
        /// Поле сортировки.
        /// </summary>
        public string OrderField
        {
            get;
            set;
        }
    }
}