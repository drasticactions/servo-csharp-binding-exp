#!/usr/bin/env bash
# Usage: ./build.sh [debug|release] [--servo-dir <path>] [--target <rust-target>]

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
CONFIGURATION="debug"
SERVO_DIR="${SCRIPT_DIR}/external/servo"
RUST_TARGET=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --servo-dir) SERVO_DIR="$2"; shift 2 ;;
        --target) RUST_TARGET="$2"; shift 2 ;;
        release|Release) CONFIGURATION="release"; shift ;;
        debug|Debug) CONFIGURATION="debug"; shift ;;
        *) shift ;;
    esac
done

echo "Building servo-ffi"

CARGO_ARGS=""
if [ "$CONFIGURATION" = "release" ]; then
    CARGO_ARGS="--release"
fi
if [ -n "$RUST_TARGET" ]; then
    CARGO_ARGS="$CARGO_ARGS --target $RUST_TARGET"
fi

cd "$SCRIPT_DIR/servo-ffi"

# Work around glslopt
# I'm building on Arch and TL;DR whatever it doesn't matter.
GLSLOPT_THREADS=""
if [ "$(uname -s)" = "Linux" ]; then
    GLSLOPT_THREADS="$(find "$HOME/.cargo/registry/src" -path '*/glslopt-*/glsl-optimizer/include/c11/threads_posix.h' 2>/dev/null | head -1)"
    if [ -n "$GLSLOPT_THREADS" ] && ! grep -q '__once_flag_defined' "$GLSLOPT_THREADS"; then
        echo "  Patching glslopt threads_posix.h for glibc >=2.39 compatibility..."
        cp "$GLSLOPT_THREADS" "$GLSLOPT_THREADS.bak"
        sed -i \
            -e 's/^#define ONCE_FLAG_INIT PTHREAD_ONCE_INIT/#ifndef __once_flag_defined\n#define ONCE_FLAG_INIT PTHREAD_ONCE_INIT\n#endif/' \
            -e 's/^typedef pthread_once_t  once_flag;/#ifndef __once_flag_defined\ntypedef pthread_once_t  once_flag;\n#endif/' \
            -e '/^\/\/ 7\.25\.2\.1$/a #ifndef __once_flag_defined' \
            -e '/^    pthread_once(flag, func);$/{n;/^}/a #endif
            }' \
            "$GLSLOPT_THREADS"
    else
        GLSLOPT_THREADS=""
    fi
fi

restore_glslopt() {
    if [ -n "${GLSLOPT_THREADS:-}" ] && [ -f "$GLSLOPT_THREADS.bak" ]; then
        mv "$GLSLOPT_THREADS.bak" "$GLSLOPT_THREADS"
        echo "  Restored original glslopt threads_posix.h"
    fi
}
trap restore_glslopt EXIT

cargo build $CARGO_ARGS

TARGET_SUBDIR="$CONFIGURATION"
if [ -n "$RUST_TARGET" ]; then
    TARGET_DIR="$SCRIPT_DIR/servo-ffi/target/$RUST_TARGET/$TARGET_SUBDIR"
else
    TARGET_DIR="$SCRIPT_DIR/servo-ffi/target/$TARGET_SUBDIR"
fi

# Determine the RID and library extension
OS="$(uname -s)"
ARCH="$(uname -m)"
case "$OS" in
    Linux*)
        RID="linux-x64"
        LIB_EXT="so"
        LIB_PREFIX="lib"
        ;;
    Darwin*)
        if [ "$ARCH" = "arm64" ]; then
            RID="osx-arm64"
        else
            RID="osx-x64"
        fi
        LIB_EXT="dylib"
        LIB_PREFIX="lib"
        ;;
    MINGW*|MSYS*|CYGWIN*)
        RID="win-x64"
        LIB_EXT="dll"
        LIB_PREFIX=""
        ;;
    *)
        echo "Unknown OS: $OS"
        exit 1
        ;;
esac

# Copy native libraries to the artifact directory
NATIVE_DIR="$SCRIPT_DIR/artifacts/runtimes/$RID/native"
mkdir -p "$NATIVE_DIR"

NATIVE_LIB="$TARGET_DIR/${LIB_PREFIX}servo_ffi.$LIB_EXT"
if [ -f "$NATIVE_LIB" ]; then
    cp "$NATIVE_LIB" "$NATIVE_DIR/"
    echo "  ${LIB_PREFIX}servo_ffi.$LIB_EXT → $RID"
fi

# Windows: also copy ANGLE DLLs
if [ "$RID" = "win-x64" ]; then
    for dll in libEGL.dll libGLESv2.dll; do
        found=$(find "$TARGET_DIR/build" -name "$dll" 2>/dev/null | head -1)
        if [ -n "$found" ]; then
            cp "$found" "$NATIVE_DIR/"
            echo "  $dll → $RID"
        fi
    done
fi