using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration.Steps
{
    /// <summary>
    /// Компактирует базу Access.
    /// </summary>
    public class CompactAccessStep
        : IMigrationStep
    {
        private readonly AccessCompactService
            _service =
                new AccessCompactService();

        public string Name =>
            "Компактим базу";

        public bool Critical =>
            true;

        public Task ExecuteAsync(
            MigrationContext context)
        {
            _service.Compact(
                context.Config.MdbPath);

            return Task.CompletedTask;
        }
    }
}