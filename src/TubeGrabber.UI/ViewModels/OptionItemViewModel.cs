// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;
using TubeGrabber.Core.Services;

namespace TubeGrabber.UI.ViewModels;

public sealed class OptionItemViewModel : ViewModelBase
{
    private object? _value;
    private bool _isVisible = true;

    public OptionItemViewModel(YtDlpOptionDefinition definition, object? initialValue)
    {
        Definition = definition;
        DefaultValue = OptionValueParser.ParseDefault(definition);
        _value = ProfileOptionValues.Normalize(initialValue) ?? DefaultValue;
    }

    public YtDlpOptionDefinition Definition { get; }
    public string Flag => Definition.Flag;
    public string Label => string.IsNullOrWhiteSpace(Definition.Label) ? Definition.Flag : Definition.Label;
    public string AutomationName => $"{Label} ({Flag})";
    public string ResetName => $"Reset {Label} to default";
    public string ChangedHelp => IsChanged ? "Changed from default" : string.Empty;
    public string Tooltip => Definition.Tooltip;
    public string ValueType => Definition.ValueType;
    public IReadOnlyList<string> Choices => Definition.Choices;
    public bool IsBool => string.Equals(ValueType, "bool", StringComparison.OrdinalIgnoreCase);
    public bool IsChoice => string.Equals(ValueType, "choice", StringComparison.OrdinalIgnoreCase);
    public bool IsStringOption => !IsBool && !IsChoice;

    /// <summary>The value of this option when a profile does not set it.</summary>
    public object? DefaultValue { get; }

    /// <summary>True when the value differs from <see cref="DefaultValue"/>; drives the accent bar and the reset button.</summary>
    public bool IsChanged => ProfileOptionValues.IsChanged(Definition, _value);

    /// <summary>Cleared by the search and "changed only" filters of the profile page.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public object? Value
    {
        get => _value;
        set
        {
            if (!SetProperty(ref _value, value))
                return;

            OnPropertyChanged(nameof(IsChanged));
            OnPropertyChanged(nameof(ChangedHelp));
        }
    }

    public bool BoolValue
    {
        get => _value is true;
        set => Value = value;
    }

    public string StringValue
    {
        get => Convert.ToString(_value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        set => Value = OptionValueParser.ParseFromString(ValueType, value);
    }

    /// <summary>
    /// Writes the default as an explicit value. Removing the key instead would let a built-in profile's template
    /// put its own value back the next time the profile is synchronised.
    /// </summary>
    public void Reset()
    {
        Value = DefaultValue;
        OnPropertyChanged(nameof(BoolValue));
        OnPropertyChanged(nameof(StringValue));
    }

    public bool Matches(string query) =>
        Label.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Flag.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Tooltip.Contains(query, StringComparison.OrdinalIgnoreCase);
}
