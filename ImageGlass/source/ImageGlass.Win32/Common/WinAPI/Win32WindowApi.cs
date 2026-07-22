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
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace ImageGlass.Win32.Common;

public static class Win32WindowApi
{

    /// <summary>
    /// Sets window backdrop.
    /// </summary>
    public static void SetWindowBackdrop(nint wndHandle, SystemBackdropType type = SystemBackdropType.Auto)
    {
        unsafe
        {
            _ = PInvoke.DwmSetWindowAttribute(new HWND(wndHandle),
               DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE,
               &type, sizeof(uint));
        }
    }


}


/// <summary>
/// <c>DWM_SYSTEMBACKDROP_TYPE</c>
/// </summary>
public enum SystemBackdropType
{
    /// <summary>
    /// <c>DWMSBT_AUTO</c>:
    /// Let OS decides.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// <c>DWMSBT_NONE</c>:
    /// No effect.
    /// </summary>
    None = 1,

    /// <summary>
    /// <c>DWMSBT_MAINWINDOW</c>:
    /// Mica effect.
    /// </summary>
    Mica = 2,

    /// <summary>
    /// <c>DWMSBT_TRANSIENTWINDOW</c>:
    /// Acrylic effect.
    /// </summary>
    Acrylic = 3,

    /// <summary>
    /// <c>DWMSBT_TABBEDWINDOW</c>:
    /// Draw the backdrop material effect corresponding to a window with a tabbed title bar.
    /// </summary>
    MicaAlt = 4,
}