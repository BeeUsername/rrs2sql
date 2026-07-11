using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration.Steps
{
    /// <summary>
    /// Выполняет SQL-патчи
    /// после конвертации базы.
    /// </summary>
    public class PatchSqlDatabaseStep
        : IMigrationStep
    {
        private readonly SqlPatchService
            _patchService =
                new SqlPatchService();

        public string Name =>
            "Патчим sql базу";

        public bool Critical =>
            true;

        public Task ExecuteAsync(
            MigrationContext context)
        {
            _patchService.ExecutePatches(
                context.Config.SqlInstance,
                context.Config.SqlDatabaseName);

            return Task.CompletedTask;
        }
    }
}