using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration.Steps
{
    public class ConvertAccessDatabaseStep
        : IMigrationStep
    {
        public string Name =>
            "Конвертируем базу";

        public bool Critical =>
            true;

        public Task ExecuteAsync(
            MigrationContext context)
        {
            var engine =
                new AccessMigrationEngine();

            engine.Run(context);

            return Task.CompletedTask;
        }
    }
}