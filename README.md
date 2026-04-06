# Experimental Servo C# Bindings

This repository is an experimental C# binding of [Servo](https://github.com/servo/servo), a Rust-based web browser engine, with controls implemented using [Avalonia](https://github.com/avaloniaui/avalonia). It consists of four components:

* `servo-ffi`, the Rust FFI for exposing what we need for C#.
* `Servo.Sharp`, the C# Binding to those headers, and the connection code needed to handle writing controls with it.
* `Servo.Sharp.Avalonia`, Avalonia-based controls that implement the bindings.
* `Servo.Sharp.Demo`, the Avalonia app demonstrating the controls.

## What this is:

A way to test Servo through Avalonia and see if it can be useful as a platform for [Avalonia.Controls.Webview](https://github.com/avaloniaui/avalonia.controls.webview). As an embedding-first engine that can target platforms, I think Servo has the potential to fill a hole for our users: both literally, by addressing airspace issues when using native controls (since we can render them as textures), and as a consistent browser engine that can run on platforms Avalonia supports. This project lets me explore the limits of what’s currently possible.

## What this is not:

* A supported library.
* Something that will go on NuGet.

If you wish to fork it and explore it more, you are more than welcome to. I would personally _not_ use any of this code as the foundation for another project, as it's very much hacked together to test an idea. But it could inspire other implementations that are thought out, or future work I do.

## How to build:

* Run the `build.sh` or `build.ps1` scripts
  * These have parameters for extra options (`Debug/Release`, enabling `--gstreamer` support). If you want a “complete” build of Servo, you should first run their bootstrap code to get a fully working setup. Read their repo for instructions on how to do that.
* Deploy the `Servo.Sharp.Demo` app.



