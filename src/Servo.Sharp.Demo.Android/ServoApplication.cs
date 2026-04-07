using System.IO;
using Android.App;
using Android.Content.Res;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Servo.Sharp.Demo.Core;

namespace Servo.Sharp.Demo.Android;

[Application]
public class ServoApplication : AvaloniaAndroidApplication<App>
{
    public ServoApplication(nint handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        var resourcePath = ExtractServoResources();
        return base.CustomizeAppBuilder(builder)
            .UseServoDefaults(resourcePath: resourcePath);
    }

    private string ExtractServoResources()
    {
        var destDir = Path.Combine(FilesDir!.AbsolutePath, "servo_resources");

        // Always extract to ensure resources are up to date
        if (Directory.Exists(destDir))
            Directory.Delete(destDir, recursive: true);

        CopyAssetDirectory(Assets!, "resources", destDir);
        return destDir;
    }

    private static void CopyAssetDirectory(AssetManager assets, string assetPath, string destPath)
    {
        Directory.CreateDirectory(destPath);

        var entries = assets.List(assetPath);
        if (entries == null) return;

        foreach (var entry in entries)
        {
            var srcPath = $"{assetPath}/{entry}";
            var dstPath = Path.Combine(destPath, entry);

            // Try to list children — if it has any, it's a directory
            var children = assets.List(srcPath);
            if (children != null && children.Length > 0)
            {
                CopyAssetDirectory(assets, srcPath, dstPath);
            }
            else
            {
                using var input = assets.Open(srcPath);
                using var output = File.Create(dstPath);
                input.CopyTo(output);
            }
        }
    }
}
