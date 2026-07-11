using System.Data.OleDb;
using RRStoSQL.Services.AccessToSql.Models;

namespace RRStoSQL.Services.AccessToSql
{
    /// <summary>
    /// Преобразует типы Access
    /// в типы SQL Server.
    /// </summary>
    public static class SqlTypeMapper
    {
        public static string GetSqlType(
            AccessColumnInfo column)
        {
            switch (column.AccessType)
            {
                case OleDbType.Boolean:
                    return "BIT";

                case OleDbType.TinyInt:
                    return "TINYINT";

                case OleDbType.SmallInt:
                    return "SMALLINT";

                case OleDbType.Integer:
                    return "INT";

                case OleDbType.BigInt:
                    return "BIGINT";

                case OleDbType.Single:
                    return "REAL";

                case OleDbType.Double:
                    return "FLOAT";

                case OleDbType.Currency:
                    return "MONEY";

                case OleDbType.Date:
                case OleDbType.DBDate:
                case OleDbType.DBTime:
                case OleDbType.DBTimeStamp:
                    return "DATETIME";

                case OleDbType.Guid:
                    return "UNIQUEIDENTIFIER";

                case OleDbType.LongVarChar:
                case OleDbType.LongVarWChar:
                    return "NVARCHAR(MAX)";

                case OleDbType.VarChar:
                case OleDbType.WChar:
                case OleDbType.VarWChar:

                    if (column.Length <= 0)
                        return "NVARCHAR(MAX)";

                    if (column.Length > 4000)
                        return "NVARCHAR(MAX)";

                    return $"NVARCHAR({column.Length})";

                default:
                    return "NVARCHAR(MAX)";
            }
        }
    }
}