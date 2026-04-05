using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Servo.Sharp.Avalonia;

internal static class FilePickerHandler
{
    public static async void HandleRequest(TopLevel topLevel, FilePickerRequestEventArgs request)
    {
        try
        {
            var filters = new List<FilePickerFileType>();
            if (request.FilterPatterns.Count > 0)
            {
                var patterns = request.FilterPatterns
                    .Select(p => p.StartsWith('.') ? "*" + p : p)
                    .ToList();
                filters.Add(new FilePickerFileType("Accepted files") { Patterns = patterns });
            }

            var options = new FilePickerOpenOptions
            {
                AllowMultiple = request.AllowMultiple,
                Title = "Select File",
            };
            if (filters.Count > 0)
                options.FileTypeFilter = filters;

            var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (result.Count > 0)
            {
                var paths = result
                    .Select(f => f.TryGetLocalPath())
                    .Where(p => p != null)
                    .ToArray();
                request.Select(paths!);
            }
            else
            {
                request.Dismiss();
            }
        }
        catch
        {
            request.Dismiss();
        }
    }
}
