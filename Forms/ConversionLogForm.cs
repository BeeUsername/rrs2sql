using System;
using System.IO;
using System.Windows.Forms;

namespace RRStoSQL.Forms
{
    public partial class ConversionLogForm : Form
    {
        // Путь к логу Bullzip
        private readonly string _logFile;

        // Таймер для периодического обновления содержимого лога
        private readonly Timer _timer =
            new Timer();

        public ConversionLogForm(
            Form owner,
            string logFile)
        {
            InitializeComponent();

            // Запоминаем путь к файлу лога
            _logFile =
                logFile;

            // Размещаем окно справа от основной формы
            StartPosition =
                FormStartPosition.Manual;

            Left =
                owner.Right + 10;

            // Выравниваем по центру основной формы по вертикали
            Top =
                owner.Top +
                (owner.Height - Height) / 2;

            // Проверяем лог раз в секунду
            _timer.Interval =
                5000;

            _timer.Tick +=
                Timer_Tick;

            _timer.Start();
        }

        private void Timer_Tick(
            object sender,
            EventArgs e)
        {
            try
            {
                // Лог ещё не появился
                if (!File.Exists(
                    _logFile))
                {
                    return;
                }

                // Читаем текущее содержимое лога
                string text =
                    File.ReadAllText(
                        _logFile);

                // Обновляем текстовое поле
                txtLog.Text =
                    text;

                // Автопрокрутка вниз
                txtLog.SelectionStart =
                    txtLog.TextLength;

                txtLog.ScrollToCaret();

                // Bullzip успешно завершил перенос
                if (text.IndexOf(
                    "Transfer completed",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _timer.Stop();

                    DialogResult =
                        DialogResult.OK;

                    Close();
                }

                // Bullzip завершился с ошибкой
                if (text.IndexOf(
                    "Error in function",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _timer.Stop();

                    MessageBox.Show(
                        "Во время конвертации обнаружена ошибка.\n\n" +
                        "Проверьте лог конвертации.",
                        "Ошибка",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    DialogResult =
                        DialogResult.Abort;

                    Close();
                }
            }
            catch
            {
                // Bullzip может писать в лог в момент чтения.
                // В этом случае просто попробуем ещё раз через секунду.
            }
        }

        private void txtLog_TextChanged(object sender, EventArgs e)
        {

        }
    }
}