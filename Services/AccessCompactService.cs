using JRO;
using System;
using System.IO;

namespace RRStoSQL.Services
{
    /// <summary>
    /// Выполняет сжатие и восстановление
    /// базы данных Microsoft Access.
    /// </summary>
    public class AccessCompactService
    {
        /// <summary>
        /// Сжимает указанную базу данных.
        /// </summary>
        /// <param name="mdbPath">
        /// Полный путь к MDB-файлу.
        /// </param>
        public void Compact(
            string mdbPath)
        {
            if (!File.Exists(mdbPath))
            {
                throw new FileNotFoundException(
                    "Файл базы данных не найден.",
                    mdbPath);
            }

            string directory =
                Path.GetDirectoryName(mdbPath);

            string fileName =
                Path.GetFileNameWithoutExtension(mdbPath);

            string extension =
                Path.GetExtension(mdbPath);

            string tempFile =
                Path.Combine(
                    directory,
                    fileName + "_tmp" + extension);

            // На всякий случай удаляем старый временный файл

            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }

                // Переименовываем исходную базу
                File.Move(
                    mdbPath,
                    tempFile);
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Не удалось получить монопольный доступ к базе данных.\r\n" +
                    "Возможно, она открыта другой программой.",
                    ex);
            }

            try
            {
                string source =
                    $"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={tempFile};";

                string destination =
                    $"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={mdbPath};Jet OLEDB:Engine Type=5;";

                //тут дубликат
                var jet =
                    new JetEngine();

                //JetEngine jet = null;

                try
                {
                    //jet = new JetEngine();

                    // Выполняем сжатие и восстановление
                    // базы данных Access.
                    jet.CompactDatabase(
                        source,
                        destination);
                }
                finally
                {
                    if (jet != null)
                    {
                        // Явно освобождаем COM-объект.
                        // Иначе JetEngine может некоторое время
                        // удерживать MDB-файл открытым.
                        System.Runtime.InteropServices.Marshal
                            .FinalReleaseComObject(jet);
                    }

                    jet = null;

                    // Дожидаемся окончательного освобождения
                    // COM-объекта сборщиком мусора.
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }


                //jet.CompactDatabase(
                //    source,
                //    destination);

                File.Delete(tempFile);
            }
            catch
            {
                // Если сжатие не удалось,
                // восстанавливаем исходную базу.
                if (!File.Exists(mdbPath) &&
                    File.Exists(tempFile))
                {
                    File.Move(
                        tempFile,
                        mdbPath);

                }

                throw;
            }
        }
    }
}
