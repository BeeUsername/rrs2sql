using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration.Steps
{
    /// <summary>
    /// Копирует строку подключения
    /// к SQL Server в буфер обмена.
    /// </summary>
    public class CopyConnectionStringStep
        : IMigrationStep
    {
        private readonly ConfigService
            _configService =
                new ConfigService();

        public string Name =>
            "Строка подключения в буфер обмена";

        public bool Critical =>
            false;

        public Task ExecuteAsync(
            MigrationContext context)
        {
            _configService.CopyConnectionString(
                context.Config);

            return Task.CompletedTask;
        }
    }
}