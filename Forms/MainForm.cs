
using System;
//using System.Collections.Generic;
//using System.ComponentModel;
//using System.Data;
//using System.Drawing;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using System.Windows.Forms;
using MigrateFromAccess.Models;
using MigrateFromAccess.Services;
using RRStoSQL.Forms.UI;
using RRStoSQL.Models;
using RRStoSQL.Services;

namespace RRStoSQL
{
    /// <summary>
    /// Главная форма приложения миграции
    /// Access -> SQL Server.
    /// </summary>
    public partial class MainForm : Form
    {
       
        /// <summary>
        /// Сервис поиска RemoteReg.exe.config
        /// в стандартных каталогах.
        /// </summary>
        private readonly ConfigFinderService _finderService;

        /// <summary>
        /// Инициализирует список шагов миграции.
        /// </summary>
        private readonly StepListInitializer _stepInitializer;

        /// <summary>
        /// Управляет отображением состояний шагов:
        /// выполняется, успешно, ошибка.
        /// </summary>
        private StepStatusManager _stepManager;

        /// <summary>
        /// Основной сервис миграции.
        /// </summary>
        private readonly MigrationService _migrationService;


        public MainForm()
        {
          

            // 1. Сначала ВСЕГДА инициализируем компоненты
            InitializeComponent();

            // 2. Если мы в режиме дизайна - выходим, НЕ создавая сервисы
            if (DesignMode)
                return;

            // 3. Теперь можно создавать сервисы (только в рантайме)
            _finderService = new ConfigFinderService();
            _stepInitializer = new StepListInitializer();
            _migrationService = new MigrationService();

            // Теперь можно выполнять остальную логику
            _stepInitializer.Initialize(listViewSteps);
            _stepManager = new StepStatusManager(listViewSteps);
            LoadConfigs();
        }
        /// <summary>
        /// Загружает найденные конфиги
        /// в список.
        /// </summary>
        private void LoadConfigs()
        {
            listBoxConfigs.Items.Clear();

            var configs = _finderService.FindConfigs();

            foreach (var config in configs)
            {
                listBoxConfigs.Items.Add(config);
            }
        }


        /// <summary>
        /// Ручное добавление конфигурационного файла
        /// RemoteReg.exe.config.
        /// </summary>
        private void btnAddConfig_Click(
            object sender,
            EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter =
                    "RemoteReg config|RemoteReg.exe.config";

                dialog.Multiselect = false;

                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                string selectedPath = dialog.FileName;

                // Проверяем, не был ли конфиг
                // добавлен ранее
                foreach (ConfigItem item in listBoxConfigs.Items)
                {
                    if (item.ConfigPath.Equals(
                        selectedPath,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        listBoxConfigs.SelectedItem = item;

                        MessageBox.Show(
                            "Конфиг уже добавлен");

                        return;
                    }
                }

                var config = new ConfigItem
                {
                    ConfigPath = selectedPath
                };

                // Добавляем конфиг в список
                listBoxConfigs.Items.Add(config);

                // Выделяем добавленный элемент
                listBoxConfigs.SelectedItem = config;
            }
        }

        private void groupBox2_Enter(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// Запускает процесс миграции.
        /// </summary>
        private async void btnConvert_Click(
            object sender,
            EventArgs e)
        {
            // Сбрасываем состояния шагов
            // перед новой миграцией
            _stepInitializer.Initialize(listViewSteps);

            // Проверяем, выбран ли конфиг
            if (listBoxConfigs.SelectedItem == null)
            {
                MessageBox.Show(
                    "Выберите конфиг");

                return;
            }

            // Блокируем кнопку на время миграции
            btnConvert.Enabled = false;

            // Формируем параметры миграции
            var options =
                new MigrationOptions
                {
                    InstallSsms =
                        chkInstallSSMS.Checked,

                    DeleteBullzip =
                        chkDeleteBullzip.Checked
                };

            try
            {
                var selectedConfig =
                    (ConfigItem)
                    listBoxConfigs.SelectedItem;

                // Запускаем миграцию
                await _migrationService.RunAsync(
                    this,
                    selectedConfig.ConfigPath,
                    options,
                    (step, status) =>
                    {
                        // Обновляем состояние шага
                        _stepManager.SetStatus(
                            step,
                            status);

                        // Обновляем интерфейс
                        Application.DoEvents();
                    });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message);
            }
            finally
            {
                // Возвращаем кнопку в рабочее состояние
                btnConvert.Enabled = true;
            }
        }

        
    }
}






