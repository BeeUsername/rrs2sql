using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration.Steps
{
    /// <summary>
    /// Переключает конфигурацию приложения
    /// с Access на SQL Server.
    /// </summary>
    public class SwitchConnectionStringsStep
        : IMigrationStep
    {
        private readonly ConfigService
            _configService =
                new ConfigService();

        public string Name =>
            "Меняем connectionstrings";

        public bool Critical =>
            true;

        public Task ExecuteAsync(
            MigrationContext context)
        {
            _configService.SwitchToSql(
                context.ConfigPath,
                context.Config);

            return Task.CompletedTask;
        }
    }
}