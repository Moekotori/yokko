using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using osu.Framework.Logging;
using Yokko.Game.Diagnostics;

namespace Yokko.Desktop.Diagnostics;

/// <summary>
/// A separate native window for live framework logs. It owns its UI thread so
/// the game loop never waits for Windows Forms painting or selection work.
/// </summary>
internal sealed class WindowsDebugConsoleWindow : IDebugConsoleWindow, IDisposable
{
    private const int historyCapacity = 5000;

    private readonly object sync = new();
    private readonly Queue<string> history = new(historyCapacity);
    private Thread uiThread;
    private Form form;
    private RichTextBox output;
    private bool requestedVisible;
    private bool disposing;

    public event Action CloseRequested;

    public WindowsDebugConsoleWindow()
    {
        Logger.NewEntry += onLoggerEntry;
    }

    public void SetVisible(bool visible)
    {
        lock (sync)
        {
            if (disposing)
                return;

            requestedVisible = visible;
            if (visible && uiThread == null)
            {
                uiThread = new Thread(runWindow)
                {
                    IsBackground = true,
                    Name = "Yokko debug console",
                };
                uiThread.SetApartmentState(ApartmentState.STA);
                uiThread.Start();
                return;
            }
        }

        invokeOnWindow(() =>
        {
            if (visible)
            {
                form.Show();
                form.Activate();
            }
            else
                form.Hide();
        });
    }

    private void runWindow()
    {
        output = new RichTextBox
        {
            BackColor = Color.FromArgb(18, 20, 26),
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            Font = new Font(FontFamily.GenericMonospace, 10),
            ForeColor = Color.Gainsboro,
            ReadOnly = true,
            WordWrap = false,
        };
        form = new Form
        {
            BackColor = output.BackColor,
            ClientSize = new Size(1100, 680),
            Controls = { output },
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            MinimumSize = new Size(640, 360),
            StartPosition = FormStartPosition.CenterScreen,
            Text = "Yokko - Live Debug Console",
        };
        form.FormClosing += onFormClosing;

        string[] initialHistory;
        lock (sync)
        {
            initialHistory = history.ToArray();
        }

        if (initialHistory.Length > 0)
            output.AppendText(string.Join(Environment.NewLine, initialHistory) + Environment.NewLine);

        bool shouldInitiallyShow;
        lock (sync)
            shouldInitiallyShow = requestedVisible && !disposing;

        if (!shouldInitiallyShow)
            form.Opacity = 0;

        form.Shown += (_, _) =>
        {
            bool shouldShow;
            lock (sync)
                shouldShow = requestedVisible && !disposing;

            if (!shouldShow)
            {
                form.Hide();
                form.Opacity = 1;
            }
        };

        Application.Run(form);

        lock (sync)
        {
            form = null;
            output = null;
            uiThread = null;
        }
    }

    private void onLoggerEntry(LogEntry entry)
    {
        if (entry == null)
            return;

        string source = entry.Target?.ToString()
                        ?? entry.LoggerName
                        ?? "Runtime";
        string line = $"{DateTimeOffset.Now:HH:mm:ss.fff}  {levelCode(entry.Level),-3}  {source,-11}  {entry.Message}";
        if (entry.Exception != null)
            line += Environment.NewLine + entry.Exception;

        lock (sync)
        {
            if (disposing)
                return;

            history.Enqueue(line);
            while (history.Count > historyCapacity)
                history.Dequeue();
        }

        invokeOnWindow(() =>
        {
            output.AppendText(line + Environment.NewLine);
            output.SelectionStart = output.TextLength;
            output.ScrollToCaret();
        });
    }

    private void onFormClosing(object sender, FormClosingEventArgs e)
    {
        lock (sync)
        {
            if (disposing)
                return;

            requestedVisible = false;
        }

        e.Cancel = true;
        form.Hide();
        CloseRequested?.Invoke();
    }

    private void invokeOnWindow(Action action)
    {
        Form currentForm;
        lock (sync)
            currentForm = form;

        if (currentForm == null || currentForm.IsDisposed || !currentForm.IsHandleCreated)
            return;

        try
        {
            currentForm.BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
            // The window was closed between the state checks and BeginInvoke.
        }
    }

    private static string levelCode(LogLevel level) => level switch
    {
        LogLevel.Error => "ERR",
        LogLevel.Important => "IMP",
        LogLevel.Debug => "DBG",
        _ => "VRB",
    };

    public void Dispose()
    {
        Logger.NewEntry -= onLoggerEntry;

        Thread thread;
        lock (sync)
        {
            if (disposing)
                return;

            disposing = true;
            thread = uiThread;
        }

        invokeOnWindow(() =>
        {
            form.FormClosing -= onFormClosing;
            form.Close();
            Application.ExitThread();
        });

        if (thread != null && thread != Thread.CurrentThread)
            thread.Join(TimeSpan.FromSeconds(2));
    }
}
