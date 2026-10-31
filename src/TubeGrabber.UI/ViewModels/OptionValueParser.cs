// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.Core.Models;

namespace TubeGrabber.UI.ViewModels;

public static class OptionValueParser
{
    public static object? ParseDefault(YtDlpOptionDefinition definition)
    {
        if (definition.DefaultValue is null)
            return IsBool(definition) ? false : string.Empty;

        if (IsBool(definition))
            return bool.TryParse(definition.DefaultValue, out var b) && b;

        return TryParseTyped(definition.ValueType, definition.DefaultValue) ?? definition.DefaultValue;
    }

    public static object? ParseFromString(string valueType, string? raw)
    {
        if (TryParseTyped(valueType, raw) is { } typed)
            return typed;

        return raw ?? string.Empty;
    }

    private static object? TryParseTyped(string valueType, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (string.Equals(valueType, "int", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(raw, out var intValue))
            return intValue;

        if (string.Equals(valueType, "double", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var doubleValue))
            return doubleValue;

        return null;
    }

    private static bool IsBool(YtDlpOptionDefinition definition) =>
        string.Equals(definition.ValueType, "bool", StringComparison.OrdinalIgnoreCase);
}
