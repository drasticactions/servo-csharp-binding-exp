using System;
using System.IO;
using Avalonia;
using Servo.Sharp;
using Servo.Sharp.Avalonia;
using Servo.Sharp.Protocols;

namespace Servo.Sharp.Demo.Core;

public static class ServoAppSetup
{
    public const string NewTabUrl = "servo:newtab";

    public static AppBuilder UseServoDefaults(this AppBuilder builder, string? resourcePath = null)
    {
        resourcePath ??= Path.Combine(AppContext.BaseDirectory, "resources");
        var protocolResourcePath = Path.Combine(resourcePath, "resource_protocol");
        var registry = CreateProtocolRegistry(protocolResourcePath);
        return builder
            .UseServo(resourcePath: resourcePath, protocolRegistry: registry)
            .WithInterFont();
    }

    public static ProtocolRegistry CreateProtocolRegistry(string protocolResourcePath)
    {
        var registry = new ProtocolRegistry();

        var resourceHandler = new ResourceProtocolHandler(protocolResourcePath);
        registry.Register("resource", resourceHandler);
        registry.Register("servo", new ServoProtocolHandler(resourceHandler));
        registry.Register("urlinfo", new UrlInfoProtocolHandler());

        return registry;
    }

    public static bool HasRegisteredScheme(string url)
    {
        var colonIndex = url.IndexOf(':');
        if (colonIndex <= 0) return false;
        var scheme = url[..colonIndex];
        return ServoLocator.Engine.RegisteredSchemes.Contains(scheme);
    }

    public static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        if (!url.Contains("://") && !url.StartsWith("data:") && !HasRegisteredScheme(url))
            url = "https://" + url;
        return url;
    }
}
