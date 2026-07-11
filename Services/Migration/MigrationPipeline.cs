using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RRStoSQL.Services.Migration
{
    public class MigrationPipeline
    {
        public List<IMigrationStep> Steps { get; }
            = new List<IMigrationStep>();

        public IMigrationStep FindStep(
            string name)
        {
            return Steps.First(
                x => x.Name == name);
        }

        /// <summary>
        /// Выполняет шаг по имени.
        /// Возвращает true, если шаг найден.
        /// </summary>
        public async Task<bool> ExecuteStepAsync(
            string stepName,
            MigrationContext context)
        {
            IMigrationStep step =
                Steps.FirstOrDefault(
                    x => x.Name == stepName);

            if (step == null)
            {
                return false;
            }

            await step.ExecuteAsync(
                context);

            return true;
        }
    }
}