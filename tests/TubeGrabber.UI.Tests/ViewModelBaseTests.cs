// Copyright (C) 2026 Florian Krämer
// SPDX-License-Identifier: GPL-3.0-or-later
// See LICENSE for details.

using TubeGrabber.UI.ViewModels;

namespace TubeGrabber.UI.Tests;

public sealed class ViewModelBaseTests
{
    private sealed class TestViewModel : ViewModelBase
    {
        private int _value;
        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }

    [Fact]
    public void SetProperty_RaisesPropertyChanged()
    {
        var vm = new TestViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TestViewModel.Value))
                raised = true;
        };

        vm.Value = 2;
        Assert.True(raised);
    }
}
