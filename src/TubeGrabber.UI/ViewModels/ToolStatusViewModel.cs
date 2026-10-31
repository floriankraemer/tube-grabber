// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.UI.ViewModels;

/// <summary>Outcome line of the last "Test" click on a binary card.</summary>
public sealed class ToolStatusViewModel : ViewModelBase
{
    private string _text = string.Empty;
    private bool _isError;

    public string Text
    {
        get => _text;
        private set
        {
            if (SetProperty(ref _text, value))
                OnPropertyChanged(nameof(HasText));
        }
    }

    public bool IsError
    {
        get => _isError;
        private set => SetProperty(ref _isError, value);
    }

    public bool HasText => Text.Length > 0;

    public void Set(string text, bool isError)
    {
        IsError = isError;
        Text = text;
    }
}
