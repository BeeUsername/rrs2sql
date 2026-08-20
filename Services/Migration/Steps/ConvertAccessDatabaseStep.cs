using RRStoSQL.Services.Migration.Engines;
using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration.Steps
{
    public class ConvertAccessDatabaseStep
        : IMigrationStep
    {
        //private readonly AccessToSqlMigrationEngine _engine =
        //    new AccessToSqlMigrationEngine();

        private readonly NativeMigrationEngine _engine =
            new NativeMigrationEngine();

        public string Name =>
            "Конвертируем базу";

        public bool Critical =>
            true;

        //public Task ExecuteAsync(
        //    MigrationContext context)
        //{
        //    _engine.Run(
        //        context);

        //    return Task.CompletedTask;
        //}
        public async Task ExecuteAsync(MigrationContext context)
        {
            await Task.Run(() =>
            {
                _engine.Run(context);
            });
        }
    }
}


