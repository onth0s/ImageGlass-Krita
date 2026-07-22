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
using System;
using System.Collections.Generic;

namespace ImageGlass.Common.ServiceProviders.FileSearchService;


/// <summary>
/// Event arguments for the <see cref="FileSearchProvider.FileSearching"/> event.
/// </summary>
public class FileSearchingEventArgs(IEnumerable<string> filePaths, bool isSearchEnded) : EventArgs
{
    /// <summary>
    /// Gets the file paths that have been enumerated.
    /// </summary>
    public IEnumerable<string> Results { get; } = filePaths;

    /// <summary>
    /// Gets a value indicating whether the search operation has completed.
    /// </summary>
    public bool IsSearchEnded => isSearchEnded;

}
