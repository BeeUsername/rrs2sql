using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration.Steps
{
    /// <summary>
    /// Проверяет наличие SQL Server Management Studio
    /// и при необходимости устанавливает её.
    /// </summary>
    public class EnsureSsmsInstalledStep
        : IMigrationStep
    {
        private readonly SsmsService
            _ssmsService =
                new SsmsService();

        public string Name =>
            "Установка sql management";

        public bool Critical =>
            false;

        public Task ExecuteAsync(
            MigrationContext context)
        {
            _ssmsService.EnsureInstalled();

            return Task.CompletedTask;
        }
    }
}