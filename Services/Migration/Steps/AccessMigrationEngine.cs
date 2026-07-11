using RRStoSQL.Forms;
using RRStoSQL.Services.Migration;
using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace RRStoSQL.Services
{
    /// <summary>
    /// Выполняет миграцию Access → SQL.
    /// Пока использует Bullzip.
    /// </summary>
    public class AccessMigrationEngine
    {
        private readonly AccessConverterService
            _converter =
                new AccessConverterService();

        private readonly BullzipService
            _bullzipService =
                new BullzipService();

        public void Run(
            MigrationContext context)
        {
            Process process =
                _converter.Convert(
                    context.Config);

            try
            {
                using (var logForm =
                    new ConversionLogForm(
                        context.Owner,
                        _converter.GetLogFilePath()))
                {
                    DialogResult result =
                        logForm.ShowDialog(
                            context.Owner);

                    if (result == DialogResult.Abort)
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            process.WaitForExit();
                        }

                        if (logForm.ConversionCanceledByUser)
                        {
                            throw new Exception(
                                "Конвертация была прервана пользователем.");
                        }

                        throw new Exception(
                            "Конвертация Bullzip завершилась с ошибкой.");
                    }
                }

                if (!process.HasExited)
                {
                    process.WaitForExit();
                }

                if (process.ExitCode != 0)
                {
                    throw new Exception(
                        "Ошибка конвертации Bullzip.\r\n\r\n" +
                        "Код завершения: " +
                        process.ExitCode);
                }
            }
            finally
            {
                try
                {
                    _bullzipService.CopyBullzipLog();
                }
                catch
                {
                    // Ошибка копирования лога
                    // не должна прерывать миграцию.
                }
            }
        }
    }
}