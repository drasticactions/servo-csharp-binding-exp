fn main() {
    csbindgen::Builder::default()
        .input_extern_file("src/lib.rs")
        .input_extern_file("src/types.rs")
        .csharp_class_name("ServoNative")
        .csharp_namespace("Servo.Sharp")
        .csharp_dll_name("servo_ffi")
        .csharp_class_accessibility("internal")
        .csharp_use_function_pointer(true)
        .csharp_use_nint_types(true)
        .generate_csharp_file("../src/Servo.Sharp/ServoNative.g.cs")
        .unwrap();
}
