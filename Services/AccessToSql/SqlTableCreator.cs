using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using RRStoSQL.Services.AccessToSql.Models;

namespace RRStoSQL.Services.AccessToSql
{
    /// <summary>
    /// Создает таблицы SQL Server
    /// на основании структуры Access базы данных.
    ///
    /// На данном этапе создаются только таблицы и поля.
    ///
    /// Первичные ключи,
    /// внешние ключи
    /// и индексы будут создаваться
    /// отдельными сервисами.
    /// </summary>
    public class SqlTableCreator
    {
        /// <summary>
        /// Выполняет создание всех таблиц.
        /// </summary>
        public void CreateTables(
            SqlConnection connection,
            List<AccessTableInfo> tables)
        {
            var mapper =
                new SqlTypeMapper();

            foreach (var table in tables)
            {
                CreateTable(
                    connection,
                    table,
                    mapper);
            }
        }

        /// <summary>
        /// Создает одну таблицу SQL Server.
        /// </summary>
        private void CreateTable(
            SqlConnection connection,
            AccessTableInfo table,
            SqlTypeMapper mapper)
        {
            string sql =
                BuildCreateTableSql(
                    table,
                    mapper);

            using (var command =
                new SqlCommand(
                    sql,
                    connection))
            {
                command.CommandTimeout = 0;

                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Формирует SQL команду
        /// CREATE TABLE.
        /// </summary>
        private string BuildCreateTableSql(
            AccessTableInfo table,
            SqlTypeMapper mapper)
        {
            var sql =
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

                sql.Append("    ");

                sql.Append(
                    $"[{column.Name}] ");

                sql.Append(
                    mapper.GetSqlType(column));

                // AutoNumber
                if (column.IsIdentity)
                {
                    sql.Append(
                        " IDENTITY(1,1)");
                }

                // NULL / NOT NULL
                sql.Append(
                    column.AllowNull
                        ? " NULL"
                        : " NOT NULL");

                if (i <
                    table.Columns.Count - 1)
                {
                    sql.Append(",");
                }

                sql.AppendLine();
            }

            sql.AppendLine(")");

            return sql.ToString();
        }
    }
}