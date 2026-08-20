
using RRStoSQL.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace RRStoSQL.Services.Migration
{
    /// <summary>
    /// Управляет жизненным циклом RemoteReg.
    /// </summary>
    internal class RemoteRegController
    {
        private Process _process;

        private const uint WM_CLOSE = 0x0010;

        // ============================================
        // Запуск УР
        // ============================================
        public void Start(
            AppConfig appConfig)
        {
            if (appConfig == null)
            {
                throw new ArgumentNullException(
                    nameof(appConfig));
            }

            if (!File.Exists(
                    appConfig.ExePath))
            {
                throw new FileNotFoundException(
                    "RemoteReg.exe не найден.",
                    appConfig.ExePath);
            }

            _process =
                new Process();

            _process.StartInfo.FileName =
                appConfig.ExePath;

            _process.StartInfo.WorkingDirectory =
                appConfig.AppFolder;

            _process.StartInfo.UseShellExecute =
                true;

            _process.Start();

            try
            {
                _process.WaitForInputIdle(
                    10000);
            }
            catch
            {
                // Некоторые приложения не поддерживают
                // WaitForInputIdle().
            }
        }

        // ============================================
        // Закрыть корректно
        // ============================================
        public void Close()
        {
            if (_process == null)
            {
                return;
            }

            if (_process.HasExited)
            {
                return;
            }

            IntPtr hWnd =
                _process.MainWindowHandle;

            if (hWnd != IntPtr.Zero)
            {
                PostMessage(
                    hWnd,
                    WM_CLOSE,
                    IntPtr.Zero,
                    IntPtr.Zero);
            }
        }

        // ============================================
        // Закрыть корректно и дождаться завершения
        // ============================================
        public void CloseGracefully(
            int timeoutMilliseconds = 30000)
        {
            if (_process == null)
            {
                return;
            }

            if (_process.HasExited)
            {
                return;
            }

            Close();

            if (!_process.WaitForExit(
                    timeoutMilliseconds))
            {
                _process.Kill();
                _process.WaitForExit();
            }
        }

        // ============================================
        // Ждем завершения
        // ============================================
        public void WaitForExit(
            int timeoutMilliseconds = 30000)
        {
            if (_process == null)
            {
                return;
            }

            if (_process.HasExited)
            {
                return;
            }

            _process.WaitForExit(
                timeoutMilliseconds);
        }

        // ============================================
        // Принудительно завершить
        // ============================================
        public void Kill()
        {
            if (_process == null)
            {
                return;
            }

            if (_process.HasExited)
            {
                return;
            }

            _process.Kill();
            _process.WaitForExit();
        }

        // ============================================
        // Запущена ли УР
        // ============================================
        public bool IsRunning
        {
            get
            {
                return
                    _process != null &&
                    !_process.HasExited;
            }
        }

        // ============================================
        // PID процесса
        // ============================================
        public int? ProcessId
        {
            get
            {
                if (_process == null)
                {
                    return null;
                }

                return _process.Id;
            }
        }

        // ============================================
        // WinAPI
        // ============================================

        [DllImport("user32.dll")]
        private static extern bool PostMessage(
            IntPtr hWnd,
            uint Msg,
            IntPtr wParam,
            IntPtr lParam);
    }
}

