use std::fs;
use std::path::PathBuf;
use std::sync::OnceLock;

use servo::resources::{Resource, ResourceReaderMethods};

pub static RESOURCE_READER: EmbeddedResourceReader = EmbeddedResourceReader {
    resource_dir: OnceLock::new(),
};

servo::submit_resource_reader!(&RESOURCE_READER);

pub struct EmbeddedResourceReader {
    resource_dir: OnceLock<PathBuf>,
}

impl EmbeddedResourceReader {
    /// Initialize the resource reader from an explicit directory path.
    pub fn init_from_path(&self, path: PathBuf) {
        self.resource_dir
            .set(path)
            .expect("Resource reader already initialized");
    }

    /// Initialize the resource reader by searching for the `resources/` directory
    /// relative to the executable.
    pub fn init_from_exe_dir(&self) {
        let exe_path = std::env::current_exe().expect("Failed to get executable path");
        let exe_dir = exe_path
            .parent()
            .expect("Executable has no parent directory");

        // Search upward for a `resources/` directory.
        let mut dir = exe_dir.to_path_buf();
        loop {
            let candidate = dir.join("resources");
            if candidate.is_dir() {
                self.resource_dir
                    .set(candidate)
                    .expect("Resource reader already initialized");
                return;
            }
            if !dir.pop() {
                break;
            }
        }

        // Fallback: check current working directory.
        let cwd = std::env::current_dir().unwrap_or_default();
        let candidate = cwd.join("resources");
        if candidate.is_dir() {
            self.resource_dir
                .set(candidate)
                .expect("Resource reader already initialized");
            return;
        }

        panic!(
            "Could not find Servo resources directory. \
             Searched from: {} and CWD: {}. \
             Please set the resource_path parameter in servo_new() or \
             place the Servo resources/ directory next to the executable.",
            exe_dir.display(),
            cwd.display()
        );
    }

    fn resource_dir(&self) -> &PathBuf {
        self.resource_dir
            .get()
            .expect("Resource reader not initialized. Call servo_new() first.")
    }
}

impl ResourceReaderMethods for EmbeddedResourceReader {
    fn read(&self, res: Resource) -> Vec<u8> {
        let path = self.resource_dir().join(res.filename());
        fs::read(&path).unwrap_or_else(|e| {
            eprintln!(
                "servo-ffi: failed to read resource {}: {e}",
                path.display()
            );
            Vec::new()
        })
    }

    fn sandbox_access_files(&self) -> Vec<PathBuf> {
        vec![]
    }

    fn sandbox_access_files_dirs(&self) -> Vec<PathBuf> {
        vec![self.resource_dir().clone()]
    }
}
