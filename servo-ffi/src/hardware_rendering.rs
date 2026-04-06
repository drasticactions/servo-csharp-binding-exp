use std::cell::Cell;
use std::rc::Rc;
use std::sync::Arc;

use dpi::PhysicalSize;
use euclid::Size2D;
use servo::{DeviceIntRect, RenderingContext, RgbaImage};
use surfman::{
    Connection, Context, ContextAttributeFlags, ContextAttributes, Device, Error, GLApi,
    SurfaceAccess, SurfaceType, Surface, SurfaceTexture,
    chains::{PreserveBuffer, SwapChain},
};

pub struct HardwareRenderingContext {
    size: Cell<PhysicalSize<u32>>,
    gleam_gl: Rc<dyn gleam::gl::Gl>,
    glow_gl: Arc<glow::Context>,
    device: std::cell::RefCell<Device>,
    context: std::cell::RefCell<Context>,
    swap_chain: SwapChain<Device>,
}

impl HardwareRenderingContext {
    pub fn new(size: PhysicalSize<u32>) -> Result<Self, Error> {
        let connection = Connection::new()?;
        let adapter = connection.create_hardware_adapter()?;
        let device = connection.create_device(&adapter)?;

        let flags = ContextAttributeFlags::ALPHA
            | ContextAttributeFlags::DEPTH
            | ContextAttributeFlags::STENCIL;
        let gl_api = connection.gl_api();
        let version = match &gl_api {
            GLApi::GLES => surfman::GLVersion { major: 3, minor: 0 },
            GLApi::GL => surfman::GLVersion { major: 3, minor: 2 },
        };
        let context_descriptor =
            device.create_context_descriptor(&ContextAttributes { flags, version })?;
        let context = device.create_context(&context_descriptor, None)?;

        #[expect(unsafe_code)]
        let gleam_gl = unsafe {
            match gl_api {
                GLApi::GL => gleam::gl::GlFns::load_with(|s| device.get_proc_address(&context, s)),
                GLApi::GLES => gleam::gl::GlesFns::load_with(|s| device.get_proc_address(&context, s)),
            }
        };

        #[expect(unsafe_code)]
        let glow_gl = unsafe {
            glow::Context::from_loader_function(|s| device.get_proc_address(&context, s))
        };

        let surfman_size = Size2D::new(size.width as i32, size.height as i32);
        let surface = device.create_surface(
            &context,
            SurfaceAccess::GPUOnly,
            SurfaceType::Generic { size: surfman_size },
        )?;
        let mut context = context;
        device
            .bind_surface_to_context(&mut context, surface)
            .map_err(|(e, _)| e)?;
        device.make_context_current(&context)?;

        let mut device = device;
        let swap_chain = SwapChain::create_attached(
            &mut device,
            &mut context,
            SurfaceAccess::GPUOnly,
        )?;
        let device = std::cell::RefCell::new(device);
        let context = std::cell::RefCell::new(context);

        Ok(HardwareRenderingContext {
            size: Cell::new(size),
            gleam_gl,
            glow_gl: Arc::new(glow_gl),
            device,
            context,
            swap_chain,
        })
    }
}

impl Drop for HardwareRenderingContext {
    fn drop(&mut self) {
        if let (Ok(mut device), Ok(mut context)) =
            (self.device.try_borrow_mut(), self.context.try_borrow_mut())
        {
            let _ = self.swap_chain.destroy(&mut device, &mut context);
            let _ = device.destroy_context(&mut context);
        }
    }
}

impl RenderingContext for HardwareRenderingContext {
    fn prepare_for_rendering(&self) {
        let device = &self.device.borrow();
        let context = &self.context.borrow();
        let framebuffer_id = device
            .context_surface_info(context)
            .unwrap_or(None)
            .and_then(|info| info.framebuffer_object)
            .map_or(0, |fb| fb.0.into());
        self.gleam_gl
            .bind_framebuffer(gleam::gl::FRAMEBUFFER, framebuffer_id);
    }

    fn read_to_image(&self, source_rectangle: DeviceIntRect) -> Option<RgbaImage> {
        let device = &self.device.borrow();
        let context = &self.context.borrow();
        let framebuffer_id = device
            .context_surface_info(context)
            .unwrap_or(None)
            .and_then(|info| info.framebuffer_object)
            .map_or(0, |fb| fb.0.into());

        use gleam::gl;
        let gl = &self.gleam_gl;
        gl.bind_framebuffer(gl::FRAMEBUFFER, framebuffer_id);
        gl.bind_vertex_array(0);

        let pixels = gl.read_pixels(
            source_rectangle.min.x,
            source_rectangle.min.y,
            source_rectangle.width(),
            source_rectangle.height(),
            gl::RGBA,
            gl::UNSIGNED_BYTE,
        );

        // Flip vertically — GL reads bottom-to-top but images are top-to-bottom
        let width = source_rectangle.width() as usize;
        let height = source_rectangle.height() as usize;
        let stride = width * 4;
        let mut flipped = vec![0u8; pixels.len()];
        for y in 0..height {
            let src_row = &pixels[y * stride..(y + 1) * stride];
            let dst_row = &mut flipped[(height - 1 - y) * stride..(height - y) * stride];
            dst_row.copy_from_slice(src_row);
        }

        RgbaImage::from_raw(width as u32, height as u32, flipped)
    }

    fn size(&self) -> PhysicalSize<u32> {
        self.size.get()
    }

    fn resize(&self, size: PhysicalSize<u32>) {
        if self.size.get() == size {
            return;
        }
        self.size.set(size);
        let device = &mut self.device.borrow_mut();
        let context = &mut self.context.borrow_mut();
        let size = Size2D::new(size.width as i32, size.height as i32);
        let _ = self.swap_chain.resize(device, context, size);
    }

    fn present(&self) {
        let device = &mut self.device.borrow_mut();
        let context = &mut self.context.borrow_mut();
        let _ = self
            .swap_chain
            .swap_buffers(device, context, PreserveBuffer::No);
    }

    fn make_current(&self) -> Result<(), Error> {
        let device = &self.device.borrow();
        let context = &self.context.borrow();
        device.make_context_current(context)
    }

    fn gleam_gl_api(&self) -> Rc<dyn gleam::gl::Gl> {
        self.gleam_gl.clone()
    }

    fn glow_gl_api(&self) -> Arc<glow::Context> {
        self.glow_gl.clone()
    }

    fn create_texture(
        &self,
        surface: Surface,
    ) -> Option<(SurfaceTexture, u32, euclid::default::Size2D<i32>)> {
        let device = &self.device.borrow();
        let context = &mut self.context.borrow_mut();
        let info = device.surface_info(&surface);
        let size = info.size;
        let surface_texture = device.create_surface_texture(context, surface).ok()?;
        let gl_texture = device
            .surface_texture_object(&surface_texture)
            .map(|tex| tex.0.get())
            .unwrap_or(0);
        Some((surface_texture, gl_texture, size))
    }

    fn destroy_texture(&self, surface_texture: SurfaceTexture) -> Option<Surface> {
        let device = &self.device.borrow();
        let context = &mut self.context.borrow_mut();
        device
            .destroy_surface_texture(context, surface_texture)
            .map_err(|(e, _)| e)
            .ok()
    }

    fn connection(&self) -> Option<Connection> {
        Some(self.device.borrow().connection())
    }
}
