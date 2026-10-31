// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using System.Text.Json;
using TubeGrabber.Core.Models;

namespace TubeGrabber.Core.Services;

public sealed class YtDlpCommandBuilder
{
    private readonly YtDlpOptionCatalog _catalog;
    private readonly ExtraArgsTokenizer _extraArgsTokenizer;

    public YtDlpCommandBuilder(YtDlpOptionCatalog catalog, ExtraArgsTokenizer extraArgsTokenizer)
    {
        _catalog = catalog;
        _extraArgsTokenizer = extraArgsTokenizer;
    }

    public IReadOnlyList<string> Build(DownloadProfile profile, string? ffmpegPath, string url) =>
        Build(profile, ffmpegPath, jsRuntimesArgument: null, url);

    public IReadOnlyList<string> Build(
        DownloadProfile profile,
        string? ffmpegPath,
        string? jsRuntimesArgument,
        string url)
    {
        var args = new List<string>
        {
            "--newline",
            "--progress",
            "--progress-template",
            YtDlpProgressParser.DownloadProgressTemplate,
            "--progress-template",
            YtDlpProgressParser.PostprocessProgressTemplate,
            "--ignore-config",
        };
        args.AddRange(YtDlpMetadataParser.BuildPrintArguments());

        if (!string.IsNullOrWhiteSpace(ffmpegPath))
        {
            args.Add("--ffmpeg-location");
            args.Add(ffmpegPath);
        }

        if (!string.IsNullOrWhiteSpace(jsRuntimesArgument))
        {
            args.Add("--js-runtimes");
            args.Add(jsRuntimesArgument);
        }

        foreach (var (flag, value) in profile.Options)
        {
            if (value is null)
                continue;

            if (_catalog.FindByFlag(flag) is null)
                continue;

            AppendOption(args, flag, value);
        }

        if (!string.IsNullOrWhiteSpace(profile.ExtraArgs))
            args.AddRange(_extraArgsTokenizer.Tokenize(profile.ExtraArgs));

        args.Add(url);
        return args;
    }

    public string BuildPreview(DownloadProfile profile, string? ffmpegPath, string url) =>
        BuildPreview(profile, ffmpegPath, jsRuntimesArgument: null, url);

    public string BuildPreview(DownloadProfile profile, string? ffmpegPath, string? jsRuntimesArgument, string url) =>
        string.Join(' ', Build(profile, ffmpegPath, jsRuntimesArgument, url).Select(EscapeForDisplay));

    private static void AppendOption(List<string> args, string flag, object value)
    {
        if (value is JsonElement element)
            value = ProfileOptionReader.ConvertJsonElement(element) ?? value;

        switch (value)
        {
            case bool boolValue:
                if (boolValue)
                    args.Add(flag);
                break;
            case string stringValue when !string.IsNullOrWhiteSpace(stringValue):
                args.Add(flag);
                args.Add(stringValue);
                break;
            case int intValue:
                args.Add(flag);
                args.Add(intValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                break;
            case long longValue:
                args.Add(flag);
                args.Add(longValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                break;
            case double doubleValue:
                args.Add(flag);
                args.Add(doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                break;
            default:
                var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    args.Add(flag);
                    args.Add(text);
                }
                break;
        }
    }

    private static string EscapeForDisplay(string arg) =>
        arg.Contains(' ', StringComparison.Ordinal) ? $"\"{arg}\"" : arg;

}
