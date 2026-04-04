use std::ffi::{c_char, c_void};

#[repr(C)]
pub struct CEventLoopWaker {
    pub user_data: *mut c_void,
    pub wake: extern "C" fn(user_data: *mut c_void),
}

unsafe impl Send for CEventLoopWaker {}
unsafe impl Sync for CEventLoopWaker {}

#[repr(C)]
pub struct ServoCallbacks {
    pub user_data: *mut c_void,

    pub on_error: Option<extern "C" fn(*mut c_void, u8, *const c_char)>,

    pub on_devtools_started: Option<extern "C" fn(*mut c_void, u16, *const c_char)>,

    pub on_console_message: Option<extern "C" fn(*mut c_void, u8, *const c_char)>,

    pub on_request_devtools_connection: Option<extern "C" fn(*mut c_void) -> u8>,
}

unsafe impl Send for ServoCallbacks {}
unsafe impl Sync for ServoCallbacks {}

#[repr(C)]
pub struct WebViewCallbacks {
    pub user_data: *mut c_void,

    pub on_new_frame_ready: Option<extern "C" fn(*mut c_void)>,
    pub on_load_status_changed: Option<extern "C" fn(*mut c_void, u8)>,
    pub on_url_changed: Option<extern "C" fn(*mut c_void, *const c_char)>,
    pub on_title_changed: Option<extern "C" fn(*mut c_void, *const c_char)>,

    pub on_cursor_changed: Option<extern "C" fn(*mut c_void, u8)>,
    pub on_focus_changed: Option<extern "C" fn(*mut c_void, u8)>,
    pub on_animating_changed: Option<extern "C" fn(*mut c_void, u8)>,
    pub on_favicon_changed: Option<extern "C" fn(*mut c_void)>,
    pub on_input_event_handled: Option<extern "C" fn(*mut c_void, u64, u8)>,
    pub on_history_changed: Option<extern "C" fn(*mut c_void, usize, usize)>,
    pub on_closed: Option<extern "C" fn(*mut c_void)>,
    pub on_fullscreen_changed: Option<extern "C" fn(*mut c_void, u8)>,

    pub on_crashed: Option<extern "C" fn(*mut c_void, *const c_char, *const c_char)>,

    pub on_console_message: Option<extern "C" fn(*mut c_void, u8, *const c_char)>,

    pub on_show_alert: Option<extern "C" fn(*mut c_void, *const c_char, usize)>,

    pub on_show_confirm: Option<extern "C" fn(*mut c_void, *const c_char, usize)>,

    pub on_show_prompt: Option<extern "C" fn(*mut c_void, *const c_char, *const c_char, usize)>,

    pub on_request_navigation: Option<extern "C" fn(*mut c_void, *const c_char, usize)>,

    pub on_request_permission: Option<extern "C" fn(*mut c_void, u8, usize)>,

    pub on_request_unload: Option<extern "C" fn(*mut c_void, usize)>,

    pub on_media_session_event: Option<extern "C" fn(*mut c_void, u8, *const c_char)>,

    pub on_show_select_element: Option<extern "C" fn(*mut c_void, *const c_char, i64, i32, i32, i32, i32, usize)>,

    pub on_show_context_menu: Option<extern "C" fn(*mut c_void, *const c_char, i32, i32, usize)>,

    pub on_request_create_new_webview: Option<extern "C" fn(*mut c_void, usize)>,

    pub on_request_authentication: Option<extern "C" fn(*mut c_void, *const c_char, u8, usize)>,

    pub on_hide_embedder_control: Option<extern "C" fn(*mut c_void)>,

    pub get_screen_geometry: Option<extern "C" fn(*mut c_void, *mut CScreenGeometry) -> u8>,
}

unsafe impl Send for WebViewCallbacks {}
unsafe impl Sync for WebViewCallbacks {}

#[repr(C)]
pub struct ClipboardCallbacks {
    pub user_data: *mut c_void,

    pub get_text: Option<extern "C" fn(*mut c_void) -> *mut c_char>,

    pub set_text: Option<extern "C" fn(*mut c_void, *const c_char)>,

    pub clear: Option<extern "C" fn(*mut c_void)>,
}

unsafe impl Send for ClipboardCallbacks {}
unsafe impl Sync for ClipboardCallbacks {}

#[repr(C)]
#[derive(Default)]
pub struct CScreenGeometry {
    pub size_width: i32,
    pub size_height: i32,
    pub available_width: i32,
    pub available_height: i32,
    pub window_x: i32,
    pub window_y: i32,
    pub window_width: i32,
    pub window_height: i32,
}
