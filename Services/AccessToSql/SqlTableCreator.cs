using System;
using System.Data.SqlClient;
using System.Text;
using RRStoSQL.Services.AccessToSql.Models;

namespace RRStoSQL.Services.AccessToSql
{
    /// <summary>
    /// Создает структуру SQL Server
    /// по описанию базы Access.
    /// </summary>
    public class SqlTableCreator
    {
        /// <summary>
        /// Создает все таблицы.
        /// </summary>
        public void CreateTables(
            SqlConnection connection,
            AccessDatabaseInfo database)
        {
            foreach (AccessTableInfo table in database.Tables)
            {
                CreateTable(
                    connection,
                    table);
            }
        }

        /// <summary>
        /// Создает одну таблицу.
        /// </summary>
        private void CreateTable(
            SqlConnection connection,
            AccessTableInfo table)
        {
            StringBuilder sql =
                new StringBuilder();

            sql.AppendLine(
                $"CREATE TABLE [{table.Name}]");

            sql.AppendLine("(");

            for (int i = 0;
                 i < table.Columns.Count;
                 i++)
            {
                AccessColumnInfo column =
                    table.Columns[i];

                sql.Append(
                    $"    [{column.Name}] ");

                sql.Append(
                    SqlTypeMapper.GetSqlType(column));

                if (!column.AllowNull)
                {
                    sql.Append(
                        " NOT NULL");
                }
                else
                {
                    sql.Append(
                        " NULL");
                }

                if (i != table.Columns.Count - 1)
                {
                    sql.Append(",");
                }

                sql.AppendLine();
            }

            sql.AppendLine(")");

            using (SqlCommand command =
                new SqlCommand(
                    sql.ToString(),
                    connection))
            {
                command.ExecuteNonQuery();
            }
        }
    }
}