// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Avalonia.Threading;

namespace UFS2Tool.GUI.Services;

/// <summary>
/// A TextWriter that redirects output to an ObservableCollection&lt;string&gt; on the UI thread.
/// Each completed line (terminated by NewLine or Flush) is added as a separate entry.
/// Carriage-return progress updates (e.g., "\r  Progress... 50%") replace the last
/// incomplete line to avoid flooding the log with progress updates.
/// </summary>
public sealed class LogTextWriter : TextWriter
{
    private readonly ObservableCollection<string> _log;
    private readonly StringBuilder _buffer = new();
    private bool _lastWasCarriageReturn;
    private bool _replaceLastLine;

    public override Encoding Encoding => Encoding.UTF8;

    public LogTextWriter(ObservableCollection<string> log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public override void Write(char value)
    {
        if (value == '\r')
        {
            // Flush current buffer content so progress is visible immediately.
            // If there was buffered text, subsequent progress replaces that entry.
            if (_buffer.Length > 0)
            {
                FlushLine();
                _replaceLastLine = true;
            }

            _lastWasCarriageReturn = true;
            return;
        }

        if (value == '\n')
        {
            FlushLine();
            return;
        }

        if (_lastWasCarriageReturn)
        {
            // Start a new buffer for the replacement line
            _buffer.Clear();
            _lastWasCarriageReturn = false;
        }

        _buffer.Append(value);
    }

    public override void Write(string? value)
    {
        if (value == null) return;

        foreach (char c in value)
            Write(c);
    }

    public override void WriteLine(string? value)
    {
        Write(value);
        FlushLine();
    }

    public override void WriteLine()
    {
        FlushLine();
    }

    public override void Flush()
    {
        if (_buffer.Length > 0)
            FlushLine();
    }

    private void FlushLine()
    {
        string line = _buffer.ToString().TrimEnd('\r', '\n');
        _buffer.Clear();
        _lastWasCarriageReturn = false;

        if (string.IsNullOrEmpty(line))
            return;

        bool replace = _replaceLastLine;
        _replaceLastLine = false;

        // Post to UI thread so ObservableCollection updates safely
        Dispatcher.UIThread.Post(() =>
        {
            if (replace && _log.Count > 0)
                _log[_log.Count - 1] = line;
            else
                _log.Add(line);
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Flush();
            _buffer.Clear();
        }
        base.Dispose(disposing);
    }
}
