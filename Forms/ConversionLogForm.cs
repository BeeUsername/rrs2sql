using System;
using System.IO;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;

namespace RRStoSQL.Forms
{
    public partial class ConversionLogForm : Form
    {
        private readonly string _logFile;
        private readonly Timer _timer = new Timer();
        private DateTime _lastWriteTime = DateTime.MinValue;
        private bool _isClosingProgrammatically = false; // 👈 Добавить флаг программного закрытия

        // true - пользователь прервал, false - успех или ошибка
        public bool ConversionCanceledByUser { get; private set; }

        public ConversionLogForm(Form owner, string logFile)
        {
            InitializeComponent();

            _logFile = logFile;

            //string text = File.ReadAllText(_logFile);
            //txtLog.Text = text;
            //txtLog.SelectionStart = txtLog.TextLength;
            //txtLog.ScrollToCaret();

            txtLog.Text = "Начинаем конвертацию Access базы..";

            StartPosition = FormStartPosition.Manual;
            Left = owner.Right + 10;
            Top = owner.Top + (owner.Height - Height) / 2;

            _timer.Interval = 5000;
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (!File.Exists(_logFile))
                    return;

                DateTime currentWriteTime = File.GetLastWriteTime(_logFile);

                if (currentWriteTime == _lastWriteTime)
                    return;

                _lastWriteTime = currentWriteTime;

                //string text = File.ReadAllText(_logFile);
                string text = ReadLogSafely();
                txtLog.Text = text;
                txtLog.SelectionStart = txtLog.TextLength;
                txtLog.ScrollToCaret();

                // ============================================
                // 1. УСПЕШНОЕ ЗАВЕРШЕНИЕ
                // ============================================
                if (text.IndexOf("Transfer completed", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _timer.Stop();
                    ConversionCanceledByUser = false; // Не отменено пользователем
                    DialogResult = DialogResult.OK;

                    _isClosingProgrammatically = true; // 👈 Программное закрытие
                    Close();
                    return;
                }

                // ============================================
                // 2. ОШИБКА КОНВЕРТАЦИИ
                // ============================================
                bool hasError = text.IndexOf("Error in function", StringComparison.OrdinalIgnoreCase) >= 0;
                bool transferAborted = text.IndexOf("Transfer was aborted", StringComparison.OrdinalIgnoreCase) >= 0;
                bool brokenRecords = text.Contains("#######");

                if (hasError || transferAborted)
                {
                    _timer.Stop();

                    string message = "Во время конвертации обнаружена ошибка.\n\nПроверьте лог конвертации.";

                    if (transferAborted && brokenRecords)
                    {
                        message = "Во время конвертации обнаружены поврежденные записи " +
                                  "в таблицах базы данных.\n\n" +
                                  "Требуется удалить или исправить поврежденные записи " +
                                  "в Access базе данных.\n\n" +
                                  "Подробности смотрите в логе конвертации.";
                    }

                    MessageBox.Show(message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    // Ошибка - это НЕ отмена пользователем
                    ConversionCanceledByUser = false;
                    DialogResult = DialogResult.Abort;

                    _isClosingProgrammatically = true; // 👈 Программное закрытие
                    Close();
                    return;
                }
            }
            catch
            {
                // Bullzip может писать в лог в момент чтения.
                // В этом случае просто попробуем ещё раз через секунду.
            }
        }

        // ============================================
        // 3. ЗАКРЫТИЕ ФОРМЫ
        // ============================================
        private void ConversionLogForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 👇 Если закрытие программное - пропускаем диалог
            if (_isClosingProgrammatically)
            {
                _isClosingProgrammatically = false;
                return;
            }

            // Пользователь нажал крестик
            if (e.CloseReason == CloseReason.UserClosing)
            {
                var result = MessageBox.Show(
                    "Прервать конвертацию базы?",
                    "Подтверждение",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                // Пользователь прервал конвертацию
                ConversionCanceledByUser = true;
                DialogResult = DialogResult.Abort;
            }
        }

        private void txtLog_TextChanged(object sender, EventArgs e)
        {
            // Можно оставить пустым или удалить
        }

        private string ReadLogSafely()
        {
            // 1. Создаем путь к временному файлу
            string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            try
            {
                // 2. Копируем лог-файл во временный
                File.Copy(_logFile, tempPath, overwrite: true);

                // 3. Читаем содержимое временного файла
                return File.ReadAllText(tempPath);
            }
            catch (IOException)
            {
                // 4. Если копирование не удалось — возвращаем null
                return null;
            }
            finally
            {
                // 5. Удаляем временный файл (всегда)
                File.Delete(tempPath);
            }
        }
    }
}




//using System;
//using System.IO;
//using System.Windows.Forms;

//namespace RRStoSQL.Forms
//{
//    public partial class ConversionLogForm : Form
//    {
//        // Путь к логу Bullzip
//        private readonly string _logFile;

//        // Таймер для периодического обновления содержимого лога
//        private readonly Timer _timer =
//            new Timer();

//        // Последнее время изменения файла лога
//        private DateTime _lastWriteTime =
//            DateTime.MinValue;
//        //Форма закрыта с ошибкой конвертации или без нее. Если пользователь закрыл окно крестиком, то ConversionCanceledByUser = true
//        public bool ConversionCanceledByUser { get; private set; }



//        public ConversionLogForm(
//            Form owner,
//            string logFile)
//        {
//            InitializeComponent();

//            // Запоминаем путь к файлу лога
//            _logFile =
//                logFile;

//            // Читаем  файл 
//            string text =
//                File.ReadAllText(
//                    _logFile);

//            // Обновляем текстовое поле
//            txtLog.Text =
//                text;

//            // Автопрокрутка вниз
//            txtLog.SelectionStart =
//                txtLog.TextLength;

//            txtLog.ScrollToCaret();

//            // Размещаем окно справа от основной формы
//            StartPosition =
//                FormStartPosition.Manual;

//            Left =
//                owner.Right + 10;

//            // Выравниваем по центру основной формы по вертикали
//            Top =
//                owner.Top +
//                (owner.Height - Height) / 2;

//            // Проверяем лог раз в секунду
//            _timer.Interval =
//                5000;

//            _timer.Tick +=
//                Timer_Tick;

//            _timer.Start();
//        }

//        private void Timer_Tick(
//            object sender,
//            EventArgs e)
//        {
//            try
//            {
//                if (!File.Exists(_logFile))
//                {
//                    return;
//                }

//                DateTime currentWriteTime =
//                    File.GetLastWriteTime(
//                        _logFile);

//                // Файл не изменился
//                if (currentWriteTime ==
//                    _lastWriteTime)
//                {
//                    return;
//                }

//                _lastWriteTime =
//                    currentWriteTime;

//                // Читаем только если файл изменился
//                string text =
//                    File.ReadAllText(
//                        _logFile);

//                // Обновляем текстовое поле
//                txtLog.Text =
//                    text;

//                // Автопрокрутка вниз
//                txtLog.SelectionStart =
//                    txtLog.TextLength;

//                txtLog.ScrollToCaret();

//                // Bullzip успешно завершил перенос
//                if (text.IndexOf(
//                    "Transfer completed",
//                    StringComparison.OrdinalIgnoreCase) >= 0)
//                {
//                    _timer.Stop();
//                    ConversionCanceledByUser = false;
//                    DialogResult =
//                        DialogResult.OK;

//                    Close();
//                }

//                // Bullzip завершился с ошибкой
//                bool hasError =
//                    text.IndexOf(
//                        "Error in function",
//                        StringComparison.OrdinalIgnoreCase) >= 0;

//                bool transferAborted =
//                    text.IndexOf(
//                        "Transfer was aborted",
//                        StringComparison.OrdinalIgnoreCase) >= 0;

//                bool brokenRecords =
//                    text.Contains("#######");

//                if (hasError || transferAborted)
//                {
//                    _timer.Stop();

//                    string message =
//                        "Во время конвертации обнаружена ошибка.\n\n" +
//                        "Проверьте лог конвертации.";

//                    // Типичная ситуация:
//                    // битые записи в Access приводят
//                    // к вставке ######## вместо данных
//                    if (transferAborted && brokenRecords)
//                    {
//                        message =
//                            "Во время конвертации обнаружены поврежденные записи " +
//                            "в таблицах базы данных.\n\n" +
//                            "Требуется удалить или исправить поврежденные записи " +
//                            "в Access базе данных.\n\n" +
//                            "Подробности смотрите в логе конвертации.";
//                    }

//                    MessageBox.Show(
//                        message,
//                        "Ошибка",
//                        MessageBoxButtons.OK,
//                        MessageBoxIcon.Error);

//                    //форма закрыта с ошибкой конвертации в логе, а не пользотвателем
//                    ConversionCanceledByUser = false;

//                    DialogResult =
//                        DialogResult.Abort;

//                    Close();
//                }
//            }
//            catch
//            {
//                // Bullzip может писать в лог в момент чтения.
//                // В этом случае просто попробуем ещё раз через секунду.
//            }
//        }

//        // Пользователь нажал крестик закрытия окна
//        private void ConversionLogForm_FormClosing(
//            object sender,
//            FormClosingEventArgs e)
//        {
//            // Пользователь нажал крестик
//            if (e.CloseReason ==
//                CloseReason.UserClosing)
//            {
//                var result =
//                    MessageBox.Show(
//                        "Прервать конвертацию базы?",
//                        "Подтверждение",
//                        MessageBoxButtons.YesNo,
//                        MessageBoxIcon.Question);

//                if (result == DialogResult.No)
//                {
//                    e.Cancel = true;
//                    return;
//                }

//                ConversionCanceledByUser = true;
//                DialogResult =
//                    DialogResult.Abort;
//            }
//        }
//        private void txtLog_TextChanged(object sender, EventArgs e)
//        {

//        }
//    }
//}