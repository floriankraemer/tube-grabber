// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

namespace TubeGrabber.Core.Models;

public sealed record SponsorSegment(string Uuid, string Category, double StartSeconds, double EndSeconds);
