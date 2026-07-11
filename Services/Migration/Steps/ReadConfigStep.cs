using RRStoSQL.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration.Steps
{
    /// <summary>
    /// Читает конфигурацию программы.
    /// </summary>
    public class ReadConfigStep : IMigrationStep
    {
        private readonly ConfigService
            _configService =
                new ConfigService();

        public string Name =>
            "Читаем конфиг";

        public bool Critical =>
            true;

        public Task ExecuteAsync(
            MigrationContext context)
        {
            //--------------------------------------------------
            // Читаем настройки приложения
            //--------------------------------------------------

            context.Config =
                _configService.Read(
                    context.Config.ConfigPath);

            //--------------------------------------------------
            // Проверяем существование MDB
            //--------------------------------------------------

            if (!File.Exists(
                    context.Config.MdbPath))
            {
                throw new Exception(
                    "MDB не найден");
            }

            return Task.CompletedTask;
        }
    }
}