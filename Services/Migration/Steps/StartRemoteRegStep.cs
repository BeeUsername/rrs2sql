using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration.Steps
{
    /// <summary>
    /// Запускает программу УР
    /// после завершения миграции.
    /// </summary>
    public class StartRemoteRegStep
        : IMigrationStep
    {
        public string Name =>
            "Запускаем УР";

        public bool Critical =>
            false;

        private readonly RemoteRegLauncher
            _launcher =
            new RemoteRegLauncher();


        public Task ExecuteAsync(
            MigrationContext context)
        {
            _launcher.StartRemoteReg(
                context.Config);

            return Task.CompletedTask;
        }
    }
}