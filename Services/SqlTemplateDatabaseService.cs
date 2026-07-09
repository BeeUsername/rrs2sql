using System;
using System.Data.SqlClient;
using System.IO;

namespace RRStoSQL.Services
{
    /// <summary>
    /// Создает новую базу данных SQL Server
    /// из шаблонной базы данных.
    ///
    /// Используется в случаях, когда
    /// Access-база практически пустая
    /// и выполнять конвертацию Bullzip
    /// не требуется.
    /// </summary>
    public class SqlTemplateDatabaseService
    {
        /// <summary>
        /// Размер Access-базы,
        /// меньше которого считается,
        /// что база практически пустая.
        /// </summary>
        private const long EmptyAccessDatabaseSize =
            3 * 1024 * 1024;

        /// <summary>
        /// Проверяет, необходимо ли
        /// создавать SQL-базу
        /// из шаблона.
        /// </summary>
        /// <param name="mdbPath">
        /// Полный путь к MDB-файлу.
        /// </param>
        /// <returns>
        /// true - использовать шаблонную SQL-базу.
        /// false - выполнять конвертацию Bullzip.
        /// </returns>
        public bool ShouldCreateFromTemplate(
            string mdbPath)
        {
            if (!File.Exists(mdbPath))
            {
                throw new FileNotFoundException(
                    "Файл базы данных не найден.",
                    mdbPath);
            }

            FileInfo file =
                new FileInfo(mdbPath);

            return
                file.Length <
                EmptyAccessDatabaseSize;
        }

        /// <summary>
        /// Создает новую SQL-базу данных
        /// из шаблонной базы.
        /// </summary>
        /// <param name="sqlInstance">
        /// Экземпляр SQL Server.
        /// </param>
        /// <param name="databaseName">
        /// Имя новой базы данных.
        /// </param>
        public void CreateDatabase(
            string sqlInstance,
            string databaseName)
        {
            string connectionString =
                $"Server={sqlInstance};" +
                $"Database=master;" +
                $"Trusted_Connection=True;" +
                $"TrustServerCertificate=True;";

            using (SqlConnection connection =
                new SqlConnection(connectionString))
            {
                connection.Open();

                // Получаем каталог DATA
                // текущего экземпляра SQL Server.
                string dataFolder =
                    GetDefaultDataFolder(
                        connection);

                // Копируем шаблонные файлы
                // в каталог DATA.
                string mdfFile;
                string ldfFile;

                CopyTemplateFiles(
                    dataFolder,
                    databaseName,
                    out mdfFile,
                    out ldfFile);

                // Присоединяем базу
                // к SQL Server.
                try
                {
                    AttachDatabase(
                        connection,
                        databaseName,
                        mdfFile,
                        ldfFile);
                }
                catch
                {
                    try
                    {
                        if (File.Exists(mdfFile))
                            File.Delete(mdfFile);

                        if (File.Exists(ldfFile))
                            File.Delete(ldfFile);
                    }
                    catch
                    {
                        // Не маскируем исходную ошибку.
                    }

                    throw;
                }
            }
        }

        

        

        

        /// <summary>
        /// Возвращает каталог DATA
        /// экземпляра SQL Server.
        /// </summary>
        /// <param name="connection">
        /// Открытое соединение с SQL Server.
        /// </param>
        /// <returns>
        /// Полный путь к каталогу DATA.
        /// </returns>
        private string GetDefaultDataFolder(
            SqlConnection connection)
        {
            const string sql =
                @"SELECT
            CAST(
                SERVERPROPERTY(
                    'InstanceDefaultDataPath')
                AS nvarchar(260))";

            using (SqlCommand command =
                new SqlCommand(
                    sql,
                    connection))
            {
                object result =
                    command.ExecuteScalar();

                if (result == null ||
                    result == DBNull.Value)
                {
                    throw new Exception(
                        "Не удалось определить каталог DATA SQL Server.");
                }

                string path =
                    result.ToString();

                if (string.IsNullOrWhiteSpace(path))
                {
                    throw new Exception(
                        "SQL Server вернул пустой каталог DATA.");
                }

                if (!Directory.Exists(path))
                {
                    throw new Exception(
                        "Каталог DATA SQL Server не существует.\r\n\r\n" +
                        path);
                }

                return path;
            }
        }

        /// <summary>
        /// Копирует шаблонную базу
        /// в каталог DATA SQL Server.
        /// </summary>
        /// <param name="dataFolder">
        /// Каталог DATA SQL Server.
        /// </param>
        /// <param name="databaseName">
        /// Имя новой базы данных.
        /// </param>
        /// <param name="mdfFile">
        /// Возвращает путь к MDF.
        /// </param>
        /// <param name="ldfFile">
        /// Возвращает путь к LDF.
        /// </param>
        private void CopyTemplateFiles(
            string dataFolder,
            string databaseName,
            out string mdfFile,
            out string ldfFile)
        {
            string templateFolder =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    @"Misc\MssqlBlankBase");

            string sourceMdf =
                Path.Combine(
                    templateFolder,
                    "citisoftDbSlis.mdf");

            string sourceLdf =
                Path.Combine(
                    templateFolder,
                    "citisoftDbSlis_log.ldf");

            if (!File.Exists(sourceMdf))
            {
                throw new FileNotFoundException(
                    "Не найден шаблон базы данных.",
                    sourceMdf);
            }

            if (!File.Exists(sourceLdf))
            {
                throw new FileNotFoundException(
                    "Не найден журнал шаблонной базы.",
                    sourceLdf);
            }

            mdfFile =
                Path.Combine(
                    dataFolder,
                    databaseName + ".mdf");

            ldfFile =
                Path.Combine(
                    dataFolder,
                    databaseName + "_log.ldf");

            // Если файлы уже существуют,
            // удаляем их.
            if (File.Exists(mdfFile))
            {
                File.Delete(mdfFile);
            }

            if (File.Exists(ldfFile))
            {
                File.Delete(ldfFile);
            }

            File.Copy(
                sourceMdf,
                mdfFile);

            File.Copy(
                sourceLdf,
                ldfFile);
        }

        /// <summary>
        /// Присоединяет шаблонную базу данных
        /// к экземпляру SQL Server.
        /// </summary>
        /// <param name="connection">
        /// Открытое соединение с SQL Server.
        /// </param>
        /// <param name="databaseName">
        /// Имя новой базы данных.
        /// </param>
        /// <param name="mdfFile">
        /// Полный путь к MDF-файлу.
        /// </param>
        /// <param name="ldfFile">
        /// Полный путь к LDF-файлу.
        /// </param>
        private void AttachDatabase(
            SqlConnection connection,
            string databaseName,
            string mdfFile,
            string ldfFile)
        {
            // Проверяем, не существует ли уже
            // база данных с таким именем.
            const string existsSql =
                @"SELECT COUNT(*)
          FROM sys.databases
          WHERE name = @DatabaseName";

            using (SqlCommand command =
                new SqlCommand(
                    existsSql,
                    connection))
            {
                command.Parameters.AddWithValue(
                    "@DatabaseName",
                    databaseName);

                int count =
                    Convert.ToInt32(
                        command.ExecuteScalar());

                if (count > 0)
                {
                    throw new Exception(
                        $"База данных \"{databaseName}\" уже существует.");
                }
            }

            // Выполняем ATTACH базы данных.
            string attachSql =
        $@"
CREATE DATABASE [{databaseName}]
ON
(
    FILENAME = N'{mdfFile}'
),
(
    FILENAME = N'{ldfFile}'
)
FOR ATTACH;
";

            using (SqlCommand command =
                new SqlCommand(
                    attachSql,
                    connection))
            {
                command.CommandTimeout = 0;

                command.ExecuteNonQuery();
            }
        }
    }
}