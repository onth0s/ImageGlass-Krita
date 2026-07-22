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
using ImageGlass.Common.ServiceProviders;

namespace ImageGlass.Linux.Common.ServiceProviders;

internal class LinuxShareProvider : IShareProvider
{

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public void ShowShare(nint windowHandle, string[] filePaths)
    {
        // Sharing is disabled on Linux for now (the Share button is hidden in the UI).
        // Linux has no general "share" sheet; the closest target is the XDG Email
        // portal, but it accepts attachments only as file descriptors (attachment_fds),
        // which the gdbus CLI cannot pass. Re-enable here once a managed D-Bus client
        // that can pass FDs is wired up.
    }

}
