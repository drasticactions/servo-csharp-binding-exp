using System.Collections.Generic;
using System.Text;
using Servo.Sharp;
using Servo.Sharp.Protocols;

namespace Servo.Sharp.Demo.Core;

public class ServoProtocolHandler : IProtocolHandler
{
    public static readonly string[] ExperimentalPrefs =
    [
        "dom_async_clipboard_enabled",
        "dom_exec_command_enabled",
        "dom_fontface_enabled",
        "dom_intersection_observer_enabled",
        "dom_navigator_protocol_handlers_enabled",
        "dom_notification_enabled",
        "dom_offscreen_canvas_enabled",
        "dom_permissions_enabled",
        "dom_webgl2_enabled",
        "dom_webgpu_enabled",
        "layout_columns_enabled",
        "layout_container_queries_enabled",
        "layout_grid_enabled",
        "layout_variable_fonts_enabled",
    ];

    private static readonly string DefaultUserAgent =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:140.0) Servo/1.0 Firefox/111.0";

    private readonly ResourceProtocolHandler _resourceHandler;

    public ServoProtocolHandler(ResourceProtocolHandler resourceHandler)
    {
        _resourceHandler = resourceHandler;
    }

    public bool IsFetchable => true;

    public IReadOnlyList<string> PrivilegedPaths => ["config", "preferences"];

    public ProtocolResponse? Load(string url)
    {
        var path = ExtractPath(url);

        return path switch
        {
            "newtab" => _resourceHandler.Load("resource:///newtab.html"),
            "config" => _resourceHandler.Load("resource:///config.html"),
            "preferences" => _resourceHandler.Load("resource:///preferences.html"),
            "license" => _resourceHandler.Load("resource:///license.html"),
            "experimental-preferences" => JsonResponse(BuildExperimentalPrefsJson()),
            "default-user-agent" => JsonResponse($"\"{DefaultUserAgent}\""),
            _ => null,
        };
    }

    private static string ExtractPath(string url)
    {
        var colonIndex = url.IndexOf(':');
        if (colonIndex < 0 || colonIndex + 1 >= url.Length)
            return "";
        return url[(colonIndex + 1)..];
    }

    private static string BuildExperimentalPrefsJson()
    {
        var sb = new StringBuilder("[");
        for (int i = 0; i < ExperimentalPrefs.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(ExperimentalPrefs[i]).Append('"');
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static ProtocolResponse JsonResponse(string json)
    {
        return new ProtocolResponse(Encoding.UTF8.GetBytes(json), "application/json");
    }
}
