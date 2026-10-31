// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Text.Json;

namespace TubeGrabber.Core.Services;

public static class ProfileOptionReader
{
    public static string? ReadString(object? value) =>
        value switch
        {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
        };

    public static bool IsTruthy(object? value) =>
        value switch
        {
            bool boolValue => boolValue,
            JsonElement { ValueKind: JsonValueKind.True } => true,
            JsonElement { ValueKind: JsonValueKind.False } => false,
            string text => text.Equals("true", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };

    public static object? ConvertJsonElement(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt32(out var i) => i,
            JsonValueKind.Number when element.TryGetDouble(out var d) => d,
            JsonValueKind.String => element.GetString(),
            _ => element.GetRawText(),
        };
}
