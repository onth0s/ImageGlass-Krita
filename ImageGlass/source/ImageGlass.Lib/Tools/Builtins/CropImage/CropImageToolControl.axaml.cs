/*
ImageGlass - A Fast, Seamless Photo Viewer
Copyright (C) 2010 - 2026 DUONG DIEU PHAP
Project homepage: https://imageglass.org

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ImageGlass.Common;
using ImageGlass.Common.Localization;
using ImageGlass.Common.Photoing;
using ImageGlass.Common.ServiceProviders;
using ImageGlass.UI;
using ImageGlass.UI.Viewer;
using ImageGlass.UI.Windowing;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace ImageGlass.Tools;

public partial class CropImageToolControl : PhControl, IToolControl
{
    // prevents dead-loop when updating NumericUpDown values from SelectionChanged
    private bool _isUpdatingSelectionUI;
    private Rect _lastSelectionArea;


    public static string TOOL_ID => "Tool_CropImage";
    public string ToolId => TOOL_ID;
    public bool HasSettingsUI => true;
    public object? Settings { get; private set; } = new CropImageConfig();
    public CropImageConfig Options => (CropImageConfig)Settings!;
    public ViewerControl Viewer { get; set; } = null!;


    public CropImageToolControl()
    {
        InitializeComponent();
    }



    #region Control Events

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // enable selection on the viewer
        Viewer.EnableSelection = true;

        // subscribe to viewer selection changes
        Viewer.SelectionChanged += Viewer_SelectionChanged;

        // subscribe to button clicks
        PART_BtnReset.Click += PART_BtnReset_Click;
        PART_BtnSave.Click += PART_BtnSave_Click;
        PART_BtnSaveAs.Click += PART_BtnSaveAs_Click;
        PART_BtnCrop.Click += PART_BtnCrop_Click;
        PART_BtnCopy.Click += PART_BtnCopy_Click;
        PART_BtnSwapRatio.Click += PART_BtnSwapRatio_Click;

        // subscribe to aspect ratio change
        PART_CmdAspectRatio.SelectionChanged += PART_CmdAspectRatio_SelectionChanged;

        // subscribe to NumericUpDown value changes
        PART_NumX.ValueChanged += NumSelection_ValueChanged;
        PART_NumY.ValueChanged += NumSelection_ValueChanged;
        PART_NumWidth.ValueChanged += NumSelection_ValueChanged;
        PART_NumHeight.ValueChanged += NumSelection_ValueChanged;

        // subscribe to custom ratio value changes
        PART_NumRatioFrom.ValueChanged += NumRatio_ValueChanged;
        PART_NumRatioTo.ValueChanged += NumRatio_ValueChanged;

        // subscribe to photo loading to restore default selection on new photo
        Viewer.PhotoLoading += Viewer_PhotoLoading;

        // load initial values from settings
        _isUpdatingSelectionUI = true;
        PART_NumRatioFrom.Value = Options.AspectRatioValues[0];
        PART_NumRatioTo.Value = Options.AspectRatioValues[1];
        PART_CmdAspectRatio.SelectedIndex = (int)Options.AspectRatio;
        _isUpdatingSelectionUI = false;

        // apply aspect ratio and load default selection
        UpdateAspectRatioValues();
        UpdateCustomRatioVisibility();
        LoadDefaultSelection();
    }


    protected override void OnUnloaded(RoutedEventArgs e)
    {
        // unsubscribe events
        Viewer.SelectionChanged -= Viewer_SelectionChanged;

        PART_BtnReset.Click -= PART_BtnReset_Click;
        PART_BtnSave.Click -= PART_BtnSave_Click;
        PART_BtnSaveAs.Click -= PART_BtnSaveAs_Click;
        PART_BtnCrop.Click -= PART_BtnCrop_Click;
        PART_BtnCopy.Click -= PART_BtnCopy_Click;
        PART_BtnSwapRatio.Click -= PART_BtnSwapRatio_Click;

        PART_CmdAspectRatio.SelectionChanged -= PART_CmdAspectRatio_SelectionChanged;

        PART_NumX.ValueChanged -= NumSelection_ValueChanged;
        PART_NumY.ValueChanged -= NumSelection_ValueChanged;
        PART_NumWidth.ValueChanged -= NumSelection_ValueChanged;
        PART_NumHeight.ValueChanged -= NumSelection_ValueChanged;

        PART_NumRatioFrom.ValueChanged -= NumRatio_ValueChanged;
        PART_NumRatioTo.ValueChanged -= NumRatio_ValueChanged;

        Viewer.PhotoLoading -= Viewer_PhotoLoading;

        // reset selection and disable selection mode
        Viewer.SourceSelection = default;
        Viewer.EnableSelection = false;

        base.OnUnloaded(e);
    }


    protected override void OnIgLanguageChanged()
    {
        base.OnIgLanguageChanged();

        PART_BtnReset.Text = Core.Lang[LangId.Tool_Crop_BtnReset];
        PART_BtnSave.Text = Core.Lang[LangId.Tool_Crop_BtnSave];
        PART_BtnSaveAs.Text = Core.Lang[LangId.Tool_Crop_BtnSaveAs];
        PART_BtnCrop.Text = Core.Lang[LangId.Tool_Crop_BtnCrop];
        PART_BtnCopy.Text = Core.Lang[LangId.Tool_Crop_BtnCopy];

        PART_CmdAspectRatio.Items[0] = Core.Lang[LangId.Tool_Crop_SelectionAspectRatio_FreeRatio];
        PART_CmdAspectRatio.Items[1] = Core.Lang[LangId.Tool_Crop_SelectionAspectRatio_Custom];
        PART_CmdAspectRatio.Items[2] = Core.Lang[LangId.Tool_Crop_SelectionAspectRatio_Original];
    }


    private void Viewer_SelectionChanged(ViewerControl sender, ViewerSelectionChangedEventArgs e)
    {
        // track the latest selection for UseTheLastSelection mode.
        _lastSelectionArea = e.SourceSelection;

        UpdateSelectionUI(e.SourceSelection);
    }


    private void PART_CmdAspectRatio_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelectionUI) return;

        Options.IsSwappedOrientation = false;
        UpdateAspectRatioValues();
        LoadDefaultSelection();
    }


    private void PART_BtnSwapRatio_Click(object? sender, RoutedEventArgs e)
    {
        SwapOrientation();
    }


    private void NumRatio_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isUpdatingSelectionUI) return;

        // update settings and viewer with new custom ratio values
        var ratioW = (int)(PART_NumRatioFrom.Value ?? 1);
        var ratioH = (int)(PART_NumRatioTo.Value ?? 1);

        Options.AspectRatioValues = [ratioW, ratioH];
        Viewer.SelectionAspectRatio = new Size(ratioW, ratioH);

        LoadDefaultSelection();
    }


    private void Viewer_PhotoLoading(ViewerControl sender, PhotoLoadingEventArgs e)
    {
        if (e.State != PhotoState.Loaded) return;

        // restore default selection when the new photo is fully loaded
        Dispatcher.UIThread.Post(() =>
        {
            UpdateAspectRatioValues();
            LoadDefaultSelection();
        });
    }


    private void NumSelection_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        // guard against dead-loop: viewer SelectionChanged -> update UI -> value changed -> update viewer
        if (_isUpdatingSelectionUI) return;

        LoadSelectionFromInputs();
    }


    private void NumericUpDown_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            LoadSelectionFromInputs();
        }
    }


    private void PART_BtnReset_Click(object? sender, RoutedEventArgs e)
    {
        Viewer.SourceSelection = default;
        Viewer.Refresh(false);
    }


    private async void PART_BtnSave_Click(object? sender, RoutedEventArgs e)
    {
        await Core.API!.RunApiAsync(API.IG_Save);

        if (Options.CloseAfterSaved)
        {
            await Core.API!.RunApiAsync(API.IG_CloseCurrentTool);
        }
    }


    private async void PART_BtnSaveAs_Click(object? sender, RoutedEventArgs e)
    {
        await Core.API!.RunApiAsync(API.IG_SaveAs);

        if (Options.CloseAfterSaved)
        {
            await Core.API!.RunApiAsync(API.IG_CloseCurrentTool);
        }
    }


    private async void PART_BtnCrop_Click(object? sender, RoutedEventArgs e)
    {
        var bitmap = Viewer.GetRenderedBitmap(true);
        var photo = new Photo(bitmap);

        await AppAPIProvider.LoadClipboardPhotoAsync(photo);
    }


    private async void PART_BtnCopy_Click(object? sender, RoutedEventArgs e)
    {
        await Core.API!.RunApiAsync(API.IG_CopyImagePixels);
    }

    #endregion // Control Events



    #region Control Methods

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public void LoadSettings(JsonElement? jsonEl)
    {
        var settings = jsonEl?.Deserialize(CropImageConfigJsonContext.Default.CropImageConfig);
        if (settings is not null)
        {
            Settings = settings;
        }

        _lastSelectionArea = Options.InitSelectedArea;
    }


    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public JsonElement? SaveSettings()
    {
        if (Options.InitSelectionType == DefaultSelectionType.UseTheLastSelection)
        {
            Options.InitSelectedArea = Viewer.SourceSelection;
        }

        var jsonEl = JsonSerializer.SerializeToElement(Options, CropImageConfigJsonContext.Default.CropImageConfig);

        return jsonEl;
    }


    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public async Task ShowSettingsWindowAsync()
    {
        var window = new CropImageSettingsWindow(Options);
        var owner = TopLevel.GetTopLevel(this) as PhWindow;
        var result = await window.ShowAsync(owner);

        if (result == DialogExitCode.OK)
        {
            Settings = window.ResultConfig;

            // re-apply aspect ratio and reload default selection
            _isUpdatingSelectionUI = true;
            PART_CmdAspectRatio.SelectedIndex = (int)Options.AspectRatio;
            _isUpdatingSelectionUI = false;

            UpdateAspectRatioValues();
            LoadDefaultSelection();
        }
    }


    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public bool HandleKeyDown(KeyEventArgs e)
    {
        // Don't intercept if modifier keys (Ctrl/Alt) are held
        var isCtrl = (e.KeyModifiers & KeyModifiers.Control) != 0;
        var isAlt = (e.KeyModifiers & KeyModifiers.Alt) != 0;

        if (isCtrl || isAlt) return false;

        switch (e.Key)
        {
            // F: Freeform ratio
            case Key.F:
                SetAspectRatio(SelectionAspectRatio.FreeRatio);
                return true;

            // O: Original ratio
            case Key.O:
                SetAspectRatio(SelectionAspectRatio.Original);
                return true;

            // X: Swap orientation
            case Key.X:
                SwapOrientation();
                return true;

            // 1: 1:1 Square
            case Key.D1:
            case Key.NumPad1:
                SetAspectRatio(SelectionAspectRatio.Ratio1_1);
                return true;

            // 2: 16:9
            case Key.D2:
            case Key.NumPad2:
                SetAspectRatio(SelectionAspectRatio.Ratio16_9);
                return true;

            // 3: 4:3
            case Key.D3:
            case Key.NumPad3:
                SetAspectRatio(SelectionAspectRatio.Ratio4_3);
                return true;

            // 4: 3:2
            case Key.D4:
            case Key.NumPad4:
                SetAspectRatio(SelectionAspectRatio.Ratio3_2);
                return true;

            // 5: 2:1
            case Key.D5:
            case Key.NumPad5:
                SetAspectRatio(SelectionAspectRatio.Ratio2_1);
                return true;

            // 6: Custom
            case Key.D6:
            case Key.NumPad6:
                SetAspectRatio(SelectionAspectRatio.Custom);
                return true;

            // C or Enter: Execute Crop
            case Key.C:
            case Key.Enter:
                if (PART_BtnCrop.IsEnabled)
                {
                    PART_BtnCrop_Click(this, new RoutedEventArgs());
                    return true;
                }
                return false;

            // Escape: Reset selection if active
            case Key.Escape:
                if (Viewer.SourceSelection.Width > 0 && Viewer.SourceSelection.Height > 0)
                {
                    Viewer.SourceSelection = default;
                    Viewer.Refresh(false);
                    return true;
                }
                return false;
        }

        return false;
    }


    /// <summary>
    /// Swaps the current aspect ratio between horizontal and vertical orientations.
    /// </summary>
    public void SwapOrientation()
    {
        var ratio = PART_CmdAspectRatio != null
            ? (SelectionAspectRatio)PART_CmdAspectRatio.SelectedIndex
            : Options.AspectRatio;
        if (ratio == SelectionAspectRatio.FreeRatio) return;

        // Swap the ratio values
        var ratioW = Options.AspectRatioValues[1];
        var ratioH = Options.AspectRatioValues[0];
        if (ratioW <= 0 || ratioH <= 0)
        {
            ratioW = 1;
            ratioH = 1;
        }

        Options.AspectRatioValues = [ratioW, ratioH];
        Options.IsSwappedOrientation = !Options.IsSwappedOrientation;

        // update custom ratio inputs if visible
        _isUpdatingSelectionUI = true;
        if (PART_NumRatioFrom != null) PART_NumRatioFrom.Value = ratioW;
        if (PART_NumRatioTo != null) PART_NumRatioTo.Value = ratioH;
        _isUpdatingSelectionUI = false;

        // update viewer
        Viewer.SelectionAspectRatio = new Size(ratioW, ratioH);

        // Flip current selection rectangle if one exists, centered on current midpoint
        var curSel = Viewer.SourceSelection;
        if (curSel.Width > 0 && curSel.Height > 0)
        {
            var srcW = (int)Viewer.BitmapSize.Width;
            var srcH = (int)Viewer.BitmapSize.Height;

            var centerX = curSel.X + curSel.Width / 2.0;
            var centerY = curSel.Y + curSel.Height / 2.0;

            // Compute new size matching the swapped ratio
            var newSize = GetSizeWithAspectRatio((int)curSel.Height, (int)curSel.Width);
            var newW = newSize.Width;
            var newH = newSize.Height;

            var newX = Math.Clamp(centerX - newW / 2.0, 0, Math.Max(0, srcW - newW));
            var newY = Math.Clamp(centerY - newH / 2.0, 0, Math.Max(0, srcH - newH));

            Viewer.SourceSelection = new Rect(newX, newY, newW, newH);
            Viewer.Refresh(false);
        }
        else
        {
            LoadDefaultSelection();
        }
    }


    /// <summary>
    /// Programmatically changes the active aspect ratio.
    /// </summary>
    private void SetAspectRatio(SelectionAspectRatio ratio)
    {
        _isUpdatingSelectionUI = true;
        if (PART_CmdAspectRatio != null) PART_CmdAspectRatio.SelectedIndex = (int)ratio;
        Options.AspectRatio = ratio;
        _isUpdatingSelectionUI = false;

        Options.IsSwappedOrientation = false;
        UpdateAspectRatioValues();
        LoadDefaultSelection();
    }


    /// <summary>
    /// Updates the viewer's <see cref="ViewerControl.SelectionAspectRatio"/>
    /// based on the current aspect ratio selection.
    /// </summary>
    private void UpdateAspectRatioValues()
    {
        var ratio = PART_CmdAspectRatio != null
            ? (SelectionAspectRatio)PART_CmdAspectRatio.SelectedIndex
            : Options.AspectRatio;
        var ratioW = Options.AspectRatioValues[0];
        var ratioH = Options.AspectRatioValues[1];

        if (ratio == SelectionAspectRatio.Original)
        {
            var srcW = (int)Viewer.BitmapSize.Width;
            var srcH = (int)Viewer.BitmapSize.Height;

            if (srcW > 0 && srcH > 0)
            {
                var results = BHelper.SimplifyFractions(srcW, srcH);
                ratioW = results[0];
                ratioH = results[1];
            }
        }
        else if (ratio == SelectionAspectRatio.Custom)
        {
            ratioW = (int)((PART_NumRatioFrom?.Value) ?? (Options.AspectRatioValues.Length > 0 ? Options.AspectRatioValues[0] : 1));
            ratioH = (int)((PART_NumRatioTo?.Value) ?? (Options.AspectRatioValues.Length > 1 ? Options.AspectRatioValues[1] : 1));

            // default to the image's simplified ratio if no custom values set
            if (ratioW <= 0 || ratioH <= 0)
            {
                var srcW = (int)Viewer.BitmapSize.Width;
                var srcH = (int)Viewer.BitmapSize.Height;

                if (srcW > 0 && srcH > 0)
                {
                    var results = BHelper.SimplifyFractions(srcW, srcH);
                    ratioW = results[0];
                    ratioH = results[1];
                }
                else
                {
                    ratioW = 1;
                    ratioH = 1;
                }
            }
        }
        else if (CropImageConfig.AspectRatioValue.TryGetValue(ratio, out var value))
        {
            ratioW = value[0];
            ratioH = value[1];
        }

        if (Options.IsSwappedOrientation && ratio != SelectionAspectRatio.FreeRatio && ratio != SelectionAspectRatio.Ratio1_1)
        {
            var temp = ratioW;
            ratioW = ratioH;
            ratioH = temp;
        }

        // update the custom ratio UI
        _isUpdatingSelectionUI = true;
        if (PART_NumRatioFrom != null) PART_NumRatioFrom.Value = ratioW;
        if (PART_NumRatioTo != null) PART_NumRatioTo.Value = ratioH;
        _isUpdatingSelectionUI = false;

        // save to settings
        Options.AspectRatio = ratio;
        Options.AspectRatioValues = [ratioW, ratioH];

        // update visibility of custom ratio controls
        UpdateCustomRatioVisibility();

        // apply to viewer
        if (ratio == SelectionAspectRatio.FreeRatio)
        {
            Viewer.SelectionAspectRatio = new Size();
        }
        else
        {
            Viewer.SelectionAspectRatio = new Size(ratioW, ratioH);
        }
    }


    /// <summary>
    /// Shows or hides the custom ratio NumericUpDown controls.
    /// They are visible for Custom and Original ratios, but only editable for Custom.
    /// </summary>
    private void UpdateCustomRatioVisibility()
    {
        var ratio = PART_CmdAspectRatio != null
            ? (SelectionAspectRatio)PART_CmdAspectRatio.SelectedIndex
            : Options.AspectRatio;

        var showCustomRatio = ratio is SelectionAspectRatio.Original
            or SelectionAspectRatio.Custom;
        if (PART_NumRatioFrom != null)
        {
            PART_NumRatioFrom.IsVisible = showCustomRatio;
            PART_NumRatioFrom.IsEnabled = ratio == SelectionAspectRatio.Custom;
        }
        if (PART_NumRatioTo != null)
        {
            PART_NumRatioTo.IsVisible = showCustomRatio;
            PART_NumRatioTo.IsEnabled = ratio == SelectionAspectRatio.Custom;
        }

        if (PART_BtnSwapRatio != null)
        {
            PART_BtnSwapRatio.IsEnabled = ratio != SelectionAspectRatio.FreeRatio;
        }
    }


    /// <summary>
    /// Loads the default selection area based on settings.
    /// </summary>
    private void LoadDefaultSelection()
    {
        var srcW = (int)Viewer.BitmapSize.Width;
        var srcH = (int)Viewer.BitmapSize.Height;

        if (srcW <= 0 || srcH <= 0) return;

        var useLastSelection = Options.InitSelectionType == DefaultSelectionType.UseTheLastSelection;

        var x = 0;
        var y = 0;
        var w = 0;
        var h = 0;

        if (useLastSelection)
        {
            x = (int)_lastSelectionArea.X;
            y = (int)_lastSelectionArea.Y;
            w = (int)_lastSelectionArea.Width;
            h = (int)_lastSelectionArea.Height;
        }
        else if (Options.InitSelectionType == DefaultSelectionType.CustomArea)
        {
            x = (int)Options.InitSelectedArea.X;
            y = (int)Options.InitSelectedArea.Y;
            w = (int)Options.InitSelectedArea.Width;
            h = (int)Options.InitSelectedArea.Height;
        }
        else
        {
            var selectPercent = Options.InitSelectionType switch
            {
                DefaultSelectionType.SelectNone => 0f,
                DefaultSelectionType.Select10Percent => 0.1f,
                DefaultSelectionType.Select20Percent => 0.2f,
                DefaultSelectionType.Select25Percent => 0.25f,
                DefaultSelectionType.Select30Percent => 0.3f,
                DefaultSelectionType.SelectOneThird => 1 / 3f,
                DefaultSelectionType.Select40Percent => 0.4f,
                DefaultSelectionType.Select50Percent => 0.5f,
                DefaultSelectionType.Select60Percent => 0.6f,
                DefaultSelectionType.SelectTwoThirds => 2 / 3f,
                DefaultSelectionType.Select70Percent => 0.7f,
                DefaultSelectionType.Select75Percent => 0.75f,
                DefaultSelectionType.Select80Percent => 0.8f,
                DefaultSelectionType.Select90Percent => 0.9f,
                DefaultSelectionType.SelectAll => 1f,
                _ => 0.5f,
            };

            w = (int)(srcW * selectPercent);
            h = (int)(srcH * selectPercent);
        }

        // update selection size according to the aspect ratio
        if (Options.AspectRatio != SelectionAspectRatio.FreeRatio
            && Options.AspectRatioValues[0] > 0
            && Options.AspectRatioValues[1] > 0)
        {
            var ratioSize = GetSizeWithAspectRatio(w, h);
            w = (int)ratioSize.Width;
            h = (int)ratioSize.Height;
        }

        // auto-center the selection (skip for UseTheLastSelection)
        if (Options.AutoCenterSelection && !useLastSelection)
        {
            x = srcW / 2 - w / 2;
            y = srcH / 2 - h / 2;
        }

        // validate bounds
        x = Math.Clamp(x, 0, Math.Max(0, srcW - w));
        y = Math.Clamp(y, 0, Math.Max(0, srcH - h));
        w = Math.Clamp(w, 0, srcW);
        h = Math.Clamp(h, 0, srcH);

        Viewer.SourceSelection = new Rect(x, y, w, h);
        Viewer.Refresh(false);
    }


    /// <summary>
    /// Computes a size that fits within the image bounds while preserving the current aspect ratio.
    /// </summary>
    private Size GetSizeWithAspectRatio(int width, int height)
    {
        var ratioW = Options.AspectRatioValues[0];
        var ratioH = Options.AspectRatioValues[1];
        if (ratioW <= 0 || ratioH <= 0) return new Size(width, height);

        var srcW = Viewer.BitmapSize.Width;
        var srcH = Viewer.BitmapSize.Height;
        if (srcW <= 0 || srcH <= 0) return new Size(width, height);

        var targetRatio = (double)ratioW / ratioH; // w / h

        var maxW = Math.Min((double)width, srcW);
        var maxH = Math.Min((double)height, srcH);
        if (maxW <= 0 || maxH <= 0)
        {
            maxW = srcW;
            maxH = srcH;
        }

        var w = maxW;
        var h = w / targetRatio;

        if (h > maxH)
        {
            h = maxH;
            w = h * targetRatio;
        }

        if (w > srcW)
        {
            w = srcW;
            h = w / targetRatio;
        }
        if (h > srcH)
        {
            h = srcH;
            w = h * targetRatio;
        }

        return new Size(Math.Max(1, Math.Round(w)), Math.Max(1, Math.Round(h)));
    }


    /// <summary>
    /// Updates the selection UI fields from the given selection rect.
    /// </summary>
    private void UpdateSelectionUI(Rect selection)
    {
        _isUpdatingSelectionUI = true;
        try
        {
            PART_NumX.Value = (decimal)selection.X;
            PART_NumY.Value = (decimal)selection.Y;
            PART_NumWidth.Value = (decimal)selection.Width;
            PART_NumHeight.Value = (decimal)selection.Height;

            UpdateActionButtonStates(selection.Width > 0 && selection.Height > 0);
        }
        finally
        {
            _isUpdatingSelectionUI = false;
        }
    }


    /// <summary>
    /// Enables or disables action buttons based on whether a selection exists.
    /// </summary>
    private void UpdateActionButtonStates(bool hasSelection)
    {
        PART_BtnSave.IsEnabled = hasSelection;
        PART_BtnSaveAs.IsEnabled = hasSelection;
        PART_BtnCrop.IsEnabled = hasSelection;
        PART_BtnCopy.IsEnabled = hasSelection;
    }


    /// <summary>
    /// Reads the current values from NumericUpDown controls and applies
    /// them to the viewer's <see cref="ViewerControl.SourceSelection"/>.
    /// </summary>
    private void LoadSelectionFromInputs()
    {
        var x = (double)(PART_NumX.Value ?? 0);
        var y = (double)(PART_NumY.Value ?? 0);
        var w = (double)(PART_NumWidth.Value ?? 0);
        var h = (double)(PART_NumHeight.Value ?? 0);

        // don't update selection with invalid values
        if (x < 0 || y < 0 || w < 0 || h < 0) return;

        var newRect = new Rect(x, y, w, h);

        if (newRect != Viewer.SourceSelection)
        {
            Viewer.SourceSelection = newRect;
            Viewer.Refresh(false);
        }
    }

    #endregion // Control Methods


}