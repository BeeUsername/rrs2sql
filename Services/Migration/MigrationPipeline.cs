using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RRStoSQL.Models;

namespace RRStoSQL.Services.Migration
{
    /// <summary>
    /// Выполняет последовательность шагов миграции.
    /// </summary>
    public class MigrationPipeline
    {
        /// <summary>
        /// Шаги миграции.
        /// </summary>
        public List<IMigrationStep> Steps { get; }
            = new List<IMigrationStep>();

        /// <summary>
        /// Ищет шаг по имени.
        /// </summary>
        public IMigrationStep FindStep(
            string name)
        {
            return Steps.FirstOrDefault(
                x => x.Name == name);
        }

        /// <summary>
        /// Выполняет один шаг.
        /// </summary>
        public async Task<bool> ExecuteStepAsync(
            string stepName,
            MigrationContext context)
        {
            IMigrationStep step =
                FindStep(stepName);

            if (step == null)
            {
                return false;
            }

            await step.ExecuteAsync(
                context);

            return true;
        }

        /// <summary>
        /// Выполняет все зарегистрированные шаги
        /// Pipeline с отображением статуса.
        /// </summary>
        public async Task ExecuteAsync(
            MigrationContext context,
            Action<string, StepStatus> updateStep)
        {
            foreach (IMigrationStep step in Steps)
            {
                updateStep?.Invoke(
                    step.Name,
                    StepStatus.Running);

                try
                {
                    await step.ExecuteAsync(
                        context);

                    updateStep?.Invoke(
                        step.Name,
                        StepStatus.Success);
                }
                catch
                {
                    updateStep?.Invoke(
                        step.Name,
                        StepStatus.Error);

                    if (step.Critical)
                    {
                        throw;
                    }
                }
            }
        }
    }
}