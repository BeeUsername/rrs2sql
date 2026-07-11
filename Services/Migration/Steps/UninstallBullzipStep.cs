using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration.Steps
{
    /// <summary>
    /// Удаляет Bullzip MS Access to MSSQL
    /// после завершения миграции.
    /// </summary>
    public class UninstallBullzipStep
        : IMigrationStep
    {
        private readonly BullzipService
            _bullzipService =
                new BullzipService();

        public string Name =>
            "Удаляем bullzip";

        public bool Critical =>
            false;

        public Task ExecuteAsync(
            MigrationContext context)
        {
            _bullzipService.Uninstall();

            return Task.CompletedTask;
        }
    }
}