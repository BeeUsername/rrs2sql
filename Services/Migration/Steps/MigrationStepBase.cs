//Пока не используется в программе.

using System.Threading.Tasks;
using RRStoSQL.Models;

namespace RRStoSQL.Services.Migration
{
    public abstract class MigrationStepBase
        : IMigrationStep
    {
        public abstract string Name { get; }

        public virtual bool Critical => true;

        public virtual bool ShouldRun(
            MigrationOptions options)
        {
            return true;
        }

        public abstract Task ExecuteAsync(
            MigrationContext context);
    }
}