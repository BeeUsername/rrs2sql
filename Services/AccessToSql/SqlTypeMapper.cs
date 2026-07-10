using System;
using System.Data.OleDb;
using RRStoSQL.Services.AccessToSql.Models;

namespace RRStoSQL.Services.AccessToSql
{
    /// <summary>
    /// Выполняет преобразование типов данных
    /// Microsoft Access в типы SQL Server.
    ///
    /// Используется при формировании
    /// CREATE TABLE.
    /// </summary>
    public class SqlTypeMapper
    {
        /// <summary>
        /// Возвращает SQL Server тип
        /// для поля Access.
        /// </summary>
        public string GetSqlType(
            AccessColumnInfo column)
        {
            // Пока используется строковое представление
            // DATA_TYPE.
            //
            // Позже заменим на OleDbType,
            // когда полностью изучим все варианты
            // схемы Access.

            switch (column.AccessType)
            {
                case OleDbType.SmallInt:
                    return "SMALLINT";

                case OleDbType.Integer:
                    return "INT";

                case OleDbType.BigInt:
                    return "BIGINT";

                case OleDbType.Boolean:
                    return "BIT";

                case OleDbType.Date:
                case OleDbType.DBDate:
                case OleDbType.DBTime:
                case OleDbType.DBTimeStamp:
                    return "DATETIME";

                case OleDbType.Currency:
                    return "DECIMAL(19,4)";

                case OleDbType.Single:
                    return "REAL";

                case OleDbType.Double:
                    return "FLOAT";

                case OleDbType.Decimal:
                case OleDbType.Numeric:
                    return "DECIMAL(18,4)";

                case OleDbType.Guid:
                    return "UNIQUEIDENTIFIER";

                case OleDbType.VarChar:
                case OleDbType.VarWChar:
                case OleDbType.WChar:

                    if (column.Length <= 0)
                        return "NVARCHAR(255)";

                    return $"NVARCHAR({column.Length})";

                case OleDbType.LongVarChar:
                case OleDbType.LongVarWChar:

                    return "NVARCHAR(MAX)";

                case OleDbType.Binary:
                case OleDbType.LongVarBinary:

                    return "VARBINARY(MAX)";

                default:

                    throw new NotSupportedException(
                        $"Тип Access '{column.AccessType}' пока не поддерживается.");
            }
        }
    }
}