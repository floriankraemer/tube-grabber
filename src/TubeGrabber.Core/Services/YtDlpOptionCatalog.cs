// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Reflection;
using System.Text.Json;
using TubeGrabber.Core.Constants;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class YtDlpOptionCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IReadOnlyList<YtDlpOptionDefinition> _options;

    public YtDlpOptionCatalog()
        : this(LoadEmbedded()) { }

    public YtDlpOptionCatalog(IReadOnlyList<YtDlpOptionDefinition> options) =>
        _options = options;

    public int CatalogVersion => SchemaVersions.OptionCatalog;

    public IReadOnlyList<YtDlpOptionDefinition> GetAll() => _options;

    public IReadOnlyList<YtDlpOptionDefinition> GetBySection(string section) =>
        _options.Where(o => string.Equals(o.Section, section, StringComparison.OrdinalIgnoreCase)).ToList();

    public IReadOnlyList<string> GetSections() =>
        _options.Select(o => o.Section).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();

    public YtDlpOptionDefinition? FindByFlag(string flag) =>
        _options.FirstOrDefault(o => string.Equals(o.Flag, flag, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<YtDlpOptionDefinition> LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .First(n => n.EndsWith("yt-dlp-common-options.json", StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Option catalog resource not found.");

        var document = JsonDocument.Parse(stream);
        var options = new List<YtDlpOptionDefinition>();

        if (document.RootElement.TryGetProperty("options", out var optionsElement))
        {
            foreach (var item in optionsElement.EnumerateArray())
            {
                options.Add(new YtDlpOptionDefinition
                {
                    Flag = item.GetProperty("flag").GetString() ?? string.Empty,
                    Label = item.TryGetProperty("label", out var label) ? label.GetString() ?? string.Empty : string.Empty,
                    Section = item.GetProperty("section").GetString() ?? string.Empty,
                    ValueType = item.GetProperty("valueType").GetString() ?? "string",
                    DefaultValue = item.TryGetProperty("default", out var def) ? ReadDefaultValue(def) : null,
                    Tooltip = item.GetProperty("tooltip").GetString() ?? string.Empty,
                    Choices = item.TryGetProperty("choices", out var choices)
                        ? choices.EnumerateArray().Select(c => c.GetString() ?? string.Empty).ToList()
                        : [],
                });
            }
        }

        return options;
    }

    private static string? ReadDefaultValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.Null => null,
            _ => element.GetRawText(),
        };
}
