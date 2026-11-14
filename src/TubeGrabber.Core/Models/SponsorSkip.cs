// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Models;

// Uuids lists every segment the jump clears, so the caller can mark all of them as skipped.
public sealed record SponsorSkip(double SeekTo, IReadOnlyList<string> Uuids);
