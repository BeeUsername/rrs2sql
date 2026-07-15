using RRStoSQL.Services.Migration.Engines;
using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration.Steps
{
    public class ConvertAccessDatabaseStep
        : IMigrationStep
    {
        private readonly NativeMigrationEngine _engine =
            new NativeMigrationEngine();

        public string Name =>
            "Конвертируем базу";

        public bool Critical =>
            true;

        public Task ExecuteAsync(
            MigrationContext context)
        {
            _engine.Run(
                context);

            return Task.CompletedTask;
        }
    }
}


//using System.Threading.Tasks;

//namespace RRStoSQL.Services.Migration.Steps
//{
//    public class ConvertAccessDatabaseStep
//        : IMigrationStep
//    {
//        public string Name =>
//            "Конвертируем базу";

//        public bool Critical =>
//            true;

//        public Task ExecuteAsync(
//            MigrationContext context)
//        {
//            var engine =
//                new AccessMigrationEngine();

//            engine.Run(context);

//            return Task.CompletedTask;
//        }
//    }
//}