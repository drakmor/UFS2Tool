// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using UFS2Tool.GUI.Services;

namespace UFS2Tool.GUI.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    public LocalizationManager Loc => LocalizationManager.Instance;

    protected ViewModelBase()
    {
        var weakRef = new WeakReference<ViewModelBase>(this);
        LocalizationManager.Instance.PropertyChanged += OnLocalizationChanged;

        void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (weakRef.TryGetTarget(out var target))
            {
                if (e.PropertyName is "Item" or "CurrentLanguage")
                    target.OnPropertyChanged(nameof(Loc));
            }
            else
            {
                LocalizationManager.Instance.PropertyChanged -= OnLocalizationChanged;
            }
        }
    }
}
