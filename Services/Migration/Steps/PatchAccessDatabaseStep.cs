using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration.Steps
{
    /// <summary>
    /// Выполняет патчи Access-базы
    /// перед конвертацией.
    /// </summary>
    public class PatchAccessDatabaseStep
        : IMigrationStep
    {
        private readonly AccessPatchService
            _accessPatchService =
                new AccessPatchService();

        public string Name =>
            "Патчим access базу";

        public bool Critical =>
            true;

        public Task ExecuteAsync(
            MigrationContext context)
        {
            _accessPatchService.ExecutePatches(
                context.Config.MdbPath);

            return Task.CompletedTask;
        }
    }
}