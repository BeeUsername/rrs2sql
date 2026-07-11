using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration.Steps
{
    /// <summary>
    /// Проверяет наличие Bullzip MS Access to MSSQL.
    /// При необходимости устанавливает его.
    /// </summary>
    public class EnsureBullzipInstalledStep
        : IMigrationStep
    {
        private readonly BullzipService
            _bullzipService =
                new BullzipService();

        public string Name =>
            "Установка bullzip msa2sql";

        public bool Critical =>
            true;

        public Task ExecuteAsync(
            MigrationContext context)
        {
            _bullzipService
                .EnsureInstalled();

            return Task.CompletedTask;
        }
    }
}