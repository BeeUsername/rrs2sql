using System;
using System.IO;

namespace RRStoSQL.Services
{
    /// <summary>
    /// Выполняет операции с базой данных Microsoft Access.
    /// </summary>
    
    //сделать методы классы статическими!!
    //сделать объект singletone
    public class AccessBaseService
    {
        /// <summary>
        /// Максимальный размер новой базы данных.
        /// База считается новой, если ее размер
        /// меньше указанного значения.
        /// </summary>
        private const long NewDatabaseSize =
            5L * 1024 * 1024;

        /// <summary>
        /// Определяет, является ли база данных новой.
        /// </summary>
        /// <param name="mdbPath">
        /// Полный путь к MDB-файлу.
        /// </param>
        /// <returns>
        /// True - база считается новой.
        /// False - база содержит данные.
        /// </returns>
        public bool IsNewDatabase(
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

            return file.Length < NewDatabaseSize;
        }

        /// <summary>
        /// Если база данных является новой,
        /// заменяет ее пустой базой из каталога Misc.
        /// </summary>
        /// <param name="mdbPath">
        /// Полный путь к MDB-файлу.
        /// </param>
        /// <returns>
        /// True - база была заменена.
        /// False - база не заменялась.
        /// </returns>
        public bool ReplaceIfNewDatabase(
            string mdbPath)
        {
            if (!IsNewDatabase(mdbPath))
            {
                return false;
            }

            ReplaceWithBlankDatabase(
                mdbPath);

            return true;
        }

        /// <summary>
        /// Заменяет указанную базу данных
        /// пустой базой из каталога Misc.
        /// </summary>
        /// <param name="mdbPath">
        /// Полный путь к MDB-файлу.
        /// </param>
        private void ReplaceWithBlankDatabase(
            string mdbPath)
        {
            string blankDatabase =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    @"Misc\Data.mdb");

            if (!File.Exists(blankDatabase))
            {
                throw new FileNotFoundException(
                    "Не найден файл шаблонной базы данных.",
                    blankDatabase);
            }

            // Удаляем существующую базу.
            File.Delete(mdbPath);

            // Копируем пустую базу.
            File.Copy(
                blankDatabase,
                mdbPath);
        }
    }
}