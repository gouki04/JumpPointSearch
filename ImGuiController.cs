using ImageMagick;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using jps;
using Veldrid;

namespace ImGuiNET
{
    /// <summary>
    /// A modified version of Veldrid.ImGui's ImGuiRenderer.
    /// Manages input for ImGui and handles rendering ImGui's DrawLists with Veldrid.
    /// </summary>
    public class ImGuiController : IDisposable
    {
        private GraphicsDevice m_GraphicsDevice;
        private bool m_IsFrameBegun;

        // Veldrid objects
        private DeviceBuffer m_VertexBuffer;
        private DeviceBuffer m_IndexBuffer;
        private DeviceBuffer m_ProjMatrixBuffer;
        private Texture m_FontTexture;
        private TextureView m_FontTextureView;
        private Shader m_VertexShader;
        private Shader m_FragmentShader;
        private ResourceLayout m_Layout;
        private ResourceLayout m_TextureLayout;
        private Pipeline m_Pipeline;
        private ResourceSet m_MainResourceSet;
        private ResourceSet m_FontTextureResourceSet;

        private readonly IntPtr m_FontAtlasID = (IntPtr)1;
        private bool m_IsCtrlDown;
        private bool m_IsShiftDown;
        private bool m_IsAltDown;
        private bool m_IsWinKeyDown;

        private int m_WindowWidth;
        private int m_WindowHeight;
        private Vector2 m_ScaleFactor = Vector2.One;

        // Image trackers
        private readonly Dictionary<string, Texture> m_TextureByPath = new Dictionary<string, Texture>();
        private readonly Dictionary<TextureView, ResourceSetInfo> m_SetsByView = new Dictionary<TextureView, ResourceSetInfo>();
        private readonly Dictionary<Texture, TextureView> m_AutoViewsByTexture = new Dictionary<Texture, TextureView>();
        private readonly Dictionary<IntPtr, ResourceSetInfo> m_ViewsById = new Dictionary<IntPtr, ResourceSetInfo>();
        private readonly List<IDisposable> m_OwnedResources = new List<IDisposable>();
        private int m_LastAssignedID = 100;

        /// <summary>
        /// Constructs a new ImGuiController.
        /// </summary>
        public ImGuiController(GraphicsDevice gd, OutputDescription output_description, int width, int height)
        {
            m_GraphicsDevice = gd;
            m_WindowWidth = width;
            m_WindowHeight = height;

            var context = ImGui.CreateContext();
            ImGui.SetCurrentContext(context);
            var fonts = ImGui.GetIO().Fonts;
            ImGui.GetIO().Fonts.AddFontDefault();

            CreateDeviceResources(gd, output_description);
            SetKeyMappings();

            SetPerFrameImGuiData(1f / 60f);

            ImGui.NewFrame();
            m_IsFrameBegun = true;
        }

        public void WindowResized(int width, int height)
        {
            m_WindowWidth = width;
            m_WindowHeight = height;
        }

        public void DestroyDeviceObjects()
        {
            Dispose();
        }

        public void CreateDeviceResources(GraphicsDevice gd, OutputDescription output_description)
        {
            m_GraphicsDevice = gd;
            var factory = gd.ResourceFactory;
            m_VertexBuffer = factory.CreateBuffer(new BufferDescription(10000, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
            m_VertexBuffer.Name = "ImGui.NET Vertex Buffer";
            m_IndexBuffer = factory.CreateBuffer(new BufferDescription(2000, BufferUsage.IndexBuffer | BufferUsage.Dynamic));
            m_IndexBuffer.Name = "ImGui.NET Index Buffer";
            RecreateFontDeviceTexture(gd);

            m_ProjMatrixBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer | BufferUsage.Dynamic));
            m_ProjMatrixBuffer.Name = "ImGui.NET Projection Buffer";

            var vertex_shader_bytes = LoadEmbeddedShaderCode(gd.ResourceFactory, "imgui-vertex", ShaderStages.Vertex);
            var fragment_shader_bytes = LoadEmbeddedShaderCode(gd.ResourceFactory, "imgui-frag", ShaderStages.Fragment);
            m_VertexShader = factory.CreateShader(new ShaderDescription(ShaderStages.Vertex, vertex_shader_bytes, "VS"));
            m_FragmentShader = factory.CreateShader(new ShaderDescription(ShaderStages.Fragment, fragment_shader_bytes, "FS"));

            var vertex_layouts = new VertexLayoutDescription[]
            {
                new VertexLayoutDescription(
                    new VertexElementDescription("in_position", VertexElementSemantic.Position, VertexElementFormat.Float2),
                    new VertexElementDescription("in_texCoord", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                    new VertexElementDescription("in_color", VertexElementSemantic.Color, VertexElementFormat.Byte4_Norm))
            };

            m_Layout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("ProjectionMatrixBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                new ResourceLayoutElementDescription("MainSampler", ResourceKind.Sampler, ShaderStages.Fragment)));
            m_TextureLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("MainTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment)));

            var pd = new GraphicsPipelineDescription(
                BlendStateDescription.SingleAlphaBlend,
                new DepthStencilStateDescription(false, false, ComparisonKind.Always),
                new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, false, true),
                PrimitiveTopology.TriangleList,
                new ShaderSetDescription(vertex_layouts, new[] { m_VertexShader, m_FragmentShader }),
                new ResourceLayout[] { m_Layout, m_TextureLayout },
                output_description);
            m_Pipeline = factory.CreateGraphicsPipeline(ref pd);

            m_MainResourceSet = factory.CreateResourceSet(new ResourceSetDescription(m_Layout,
                m_ProjMatrixBuffer,
                gd.PointSampler));

            m_FontTextureResourceSet = factory.CreateResourceSet(new ResourceSetDescription(m_TextureLayout, m_FontTextureView));
        }

        public uint RGBA2Int(byte r, byte g, byte b, byte a)
        {
            return (uint)(a << 24 | b << 16 | g << 8 | r);
        }

        public uint Color2Int(MagickColor color)
        {
            return RGBA2Int((byte)color.R, (byte)color.G, (byte)color.B, (byte)color.A);
        }

        public Texture GetOrCreateTexture(string path)
        {
            if (!m_TextureByPath.TryGetValue(path, out var texture)) {
                using (var stream = File.OpenRead(path)) {
                    using (var image = new MagickImage(stream)) {
                        var pixels = image.GetPixels();
                        var width = (uint)image.Width;
                        var height = (uint)image.Height;

                        var bytes = new uint[width * height];
                        for (var x = 0; x < width; ++x) {
                            for (var y = 0; y < height; ++y) {
                                bytes[y * width + x] = Color2Int(pixels[x, y].ToColor());
                            }
                        }

                        var texture_desc = TextureDescription.Texture2D(width, height, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled);
                        texture = m_GraphicsDevice.ResourceFactory.CreateTexture(texture_desc);
                        
                        m_GraphicsDevice.UpdateTexture<uint>(texture, bytes, 0, 0, 0, width, height, 1, 0, 0);

                        m_TextureByPath.Add(path, texture);
                    }
                }
            }

            return texture;
        }

        /// <summary>
        /// Gets or creates a handle for a texture to be drawn with ImGui.
        /// Pass the returned handle to Image() or ImageButton().
        /// </summary>
        public IntPtr GetOrCreateImGuiBinding(ResourceFactory factory, TextureView texture_view)
        {
            if (!m_SetsByView.TryGetValue(texture_view, out var rsi)) {
                var resource_set = factory.CreateResourceSet(new ResourceSetDescription(m_TextureLayout, texture_view));
                rsi = new ResourceSetInfo(GetNextImGuiBindingID(), resource_set);

                m_SetsByView.Add(texture_view, rsi);
                m_ViewsById.Add(rsi.ImGuiBinding, rsi);
                m_OwnedResources.Add(resource_set);
            }

            return rsi.ImGuiBinding;
        }

        public IntPtr GetOrCreateImGuiBinding(TextureView texture_view)
        {
            return GetOrCreateImGuiBinding(m_GraphicsDevice.ResourceFactory, texture_view);
        }

        private IntPtr GetNextImGuiBindingID()
        {
            var newID = m_LastAssignedID++;
            return (IntPtr)newID;
        }

        /// <summary>
        /// Gets or creates a handle for a texture to be drawn with ImGui.
        /// Pass the returned handle to Image() or ImageButton().
        /// </summary>
        public IntPtr GetOrCreateImGuiBinding(ResourceFactory factory, Texture texture)
        {
            if (!m_AutoViewsByTexture.TryGetValue(texture, out var textureView)) {
                textureView = factory.CreateTextureView(texture);
                m_AutoViewsByTexture.Add(texture, textureView);
                m_OwnedResources.Add(textureView);
            }

            return GetOrCreateImGuiBinding(factory, textureView);
        }

        public IntPtr GetOrCreateImGuiBinding(Texture texture)
        {
            return GetOrCreateImGuiBinding(m_GraphicsDevice.ResourceFactory, texture);
        }

        /// <summary>
        /// Retrieves the shader texture binding for the given helper handle.
        /// </summary>
        public ResourceSet GetImageResourceSet(IntPtr imGui_binding)
        {
            if (!m_ViewsById.TryGetValue(imGui_binding, out var tvi)) {
                throw new InvalidOperationException("No registered ImGui binding with id " + imGui_binding.ToString());
            }

            return tvi.ResourceSet;
        }

        public void ClearCachedImageResources()
        {
            foreach (var resource in m_OwnedResources) {
                resource.Dispose();
            }

            m_OwnedResources.Clear();
            m_SetsByView.Clear();
            m_ViewsById.Clear();
            m_AutoViewsByTexture.Clear();
            m_LastAssignedID = 100;
        }

        private byte[] LoadEmbeddedShaderCode(ResourceFactory factory, string name, ShaderStages stage)
        {
            switch (factory.BackendType) {
                case GraphicsBackend.Direct3D11: {
                        var resourceName = string.Format("jps.Shaders.HLSL.{0}.hlsl.bytes", name);
                        return GetEmbeddedResourceBytes(resourceName);
                    }
                case GraphicsBackend.OpenGL: {
                        var resourceName = string.Format("jps.Shaders.GLSL.{0}.glsl", name);
                        return GetEmbeddedResourceBytes(resourceName);
                    }
                case GraphicsBackend.Vulkan: {
                        var resourceName = string.Format("jps.Shaders.SPIR_V.{0}.spv", name);
                        return GetEmbeddedResourceBytes(resourceName);
                    }
                case GraphicsBackend.Metal: {
                        var resourceName = string.Format("jps.Shaders.Metal.{0}.metallib", name);
                        return GetEmbeddedResourceBytes(resourceName);
                    }
                default:
                    throw new NotImplementedException();
            }
        }

        private byte[] GetEmbeddedResourceBytes(string resource_name)
        {
            var assembly = typeof(ImGuiController).Assembly;
            using (var s = assembly.GetManifestResourceStream(resource_name)) {
                var ret = new byte[s.Length];
                s.Read(ret, 0, (int)s.Length);
                return ret;
            }
        }

        /// <summary>
        /// Recreates the device texture used to render text.
        /// </summary>
        public void RecreateFontDeviceTexture(GraphicsDevice gd)
        {
            var io = ImGui.GetIO();
            // Build
            IntPtr pixels;
            int width, height, bytes_per_pixel;
            io.Fonts.GetTexDataAsRGBA32(out pixels, out width, out height, out bytes_per_pixel);
            // Store our identifier
            io.Fonts.SetTexID(m_FontAtlasID);

            m_FontTexture = gd.ResourceFactory.CreateTexture(TextureDescription.Texture2D(
                (uint)width,
                (uint)height,
                1,
                1,
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.Sampled));
            m_FontTexture.Name = "ImGui.NET Font Texture";
            gd.UpdateTexture(
                m_FontTexture,
                pixels,
                (uint)(bytes_per_pixel * width * height),
                0,
                0,
                0,
                (uint)width,
                (uint)height,
                1,
                0,
                0);
            m_FontTextureView = gd.ResourceFactory.CreateTextureView(m_FontTexture);

            io.Fonts.ClearTexData();
        }

        /// <summary>
        /// Renders the ImGui draw list data.
        /// This method requires a <see cref="GraphicsDevice"/> because it may create new DeviceBuffers if the size of vertex
        /// or index data has increased beyond the capacity of the existing buffers.
        /// A <see cref="CommandList"/> is needed to submit drawing and resource update commands.
        /// </summary>
        public void Render(GraphicsDevice gd, CommandList cl)
        {
            if (m_IsFrameBegun) {
                m_IsFrameBegun = false;
                ImGui.Render();
                RenderImDrawData(ImGui.GetDrawData(), gd, cl);
            }
        }

        /// <summary>
        /// Updates ImGui input and IO configuration state.
        /// </summary>
        public void Update(float delta_seconds, InputSnapshot snapshot)
        {
            if (m_IsFrameBegun) {
                ImGui.Render();
            }

            SetPerFrameImGuiData(delta_seconds);
            UpdateImGuiInput(snapshot);

            m_IsFrameBegun = true;
            ImGui.NewFrame();
        }

        /// <summary>
        /// Sets per-frame data based on the associated window.
        /// This is called by Update(float).
        /// </summary>
        private void SetPerFrameImGuiData(float delta_seconds)
        {
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2(
                m_WindowWidth / m_ScaleFactor.X,
                m_WindowHeight / m_ScaleFactor.Y);
            io.DisplayFramebufferScale = m_ScaleFactor;
            io.DeltaTime = delta_seconds; // DeltaTime is in seconds.
        }

        private void UpdateImGuiInput(InputSnapshot snapshot)
        {
            var io = ImGui.GetIO();

            var mouse_position = snapshot.MousePosition;

            // Determine if any of the mouse buttons were pressed during this snapshot period, even if they are no longer held.
            var is_left_pressed = false;
            var is_middle_pressed = false;
            var is_right_pressed = false;
            foreach (var me in snapshot.MouseEvents) {
                if (me.Down) {
                    switch (me.MouseButton) {
                        case MouseButton.Left:
                            is_left_pressed = true;
                            break;
                        case MouseButton.Middle:
                            is_middle_pressed = true;
                            break;
                        case MouseButton.Right:
                            is_right_pressed = true;
                            break;
                    }
                }
            }

            io.MouseDown[0] = is_left_pressed || snapshot.IsMouseDown(MouseButton.Left);
            io.MouseDown[1] = is_right_pressed || snapshot.IsMouseDown(MouseButton.Right);
            io.MouseDown[2] = is_middle_pressed || snapshot.IsMouseDown(MouseButton.Middle);
            io.MousePos = mouse_position;
            io.MouseWheel = snapshot.WheelDelta;

            var is_key_char_presses = snapshot.KeyCharPresses;
            for (var i = 0; i < is_key_char_presses.Count; i++) {
                var c = is_key_char_presses[i];
                io.AddInputCharacter(c);
            }

            var key_events = snapshot.KeyEvents;
            for (var i = 0; i < key_events.Count; i++) {
                var key_event = key_events[i];
                io.KeysDown[(int)key_event.Key] = key_event.Down;
                if (key_event.Key == Key.ControlLeft) {
                    m_IsCtrlDown = key_event.Down;
                }
                if (key_event.Key == Key.ShiftLeft) {
                    m_IsShiftDown = key_event.Down;
                }
                if (key_event.Key == Key.AltLeft) {
                    m_IsAltDown = key_event.Down;
                }
                if (key_event.Key == Key.WinLeft) {
                    m_IsWinKeyDown = key_event.Down;
                }
            }

            io.KeyCtrl = m_IsCtrlDown;
            io.KeyAlt = m_IsAltDown;
            io.KeyShift = m_IsShiftDown;
            io.KeySuper = m_IsWinKeyDown;
        }

        private static void SetKeyMappings()
        {
            var io = ImGui.GetIO();
            io.KeyMap[(int)ImGuiKey.Tab] = (int)Key.Tab;
            io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)Key.Left;
            io.KeyMap[(int)ImGuiKey.RightArrow] = (int)Key.Right;
            io.KeyMap[(int)ImGuiKey.UpArrow] = (int)Key.Up;
            io.KeyMap[(int)ImGuiKey.DownArrow] = (int)Key.Down;
            io.KeyMap[(int)ImGuiKey.PageUp] = (int)Key.PageUp;
            io.KeyMap[(int)ImGuiKey.PageDown] = (int)Key.PageDown;
            io.KeyMap[(int)ImGuiKey.Home] = (int)Key.Home;
            io.KeyMap[(int)ImGuiKey.End] = (int)Key.End;
            io.KeyMap[(int)ImGuiKey.Delete] = (int)Key.Delete;
            io.KeyMap[(int)ImGuiKey.Backspace] = (int)Key.BackSpace;
            io.KeyMap[(int)ImGuiKey.Enter] = (int)Key.Enter;
            io.KeyMap[(int)ImGuiKey.Escape] = (int)Key.Escape;
            io.KeyMap[(int)ImGuiKey.A] = (int)Key.A;
            io.KeyMap[(int)ImGuiKey.C] = (int)Key.C;
            io.KeyMap[(int)ImGuiKey.V] = (int)Key.V;
            io.KeyMap[(int)ImGuiKey.X] = (int)Key.X;
            io.KeyMap[(int)ImGuiKey.Y] = (int)Key.Y;
            io.KeyMap[(int)ImGuiKey.Z] = (int)Key.Z;
        }

        private void RenderImDrawData(ImDrawDataPtr draw_data, GraphicsDevice gd, CommandList cl)
        {
            uint vertex_offset_in_vertices = 0;
            uint index_offset_in_elements = 0;

            if (draw_data.CmdListsCount == 0) {
                return;
            }

            var total_vb_size = (uint)(draw_data.TotalVtxCount * Unsafe.SizeOf<ImDrawVert>());
            if (total_vb_size > m_VertexBuffer.SizeInBytes) {
                gd.DisposeWhenIdle(m_VertexBuffer);
                m_VertexBuffer = gd.ResourceFactory.CreateBuffer(new BufferDescription((uint)(total_vb_size * 1.5f), BufferUsage.VertexBuffer | BufferUsage.Dynamic));
            }

            var total_ib_size = (uint)(draw_data.TotalIdxCount * sizeof(ushort));
            if (total_ib_size > m_IndexBuffer.SizeInBytes) {
                gd.DisposeWhenIdle(m_IndexBuffer);
                m_IndexBuffer = gd.ResourceFactory.CreateBuffer(new BufferDescription((uint)(total_ib_size * 1.5f), BufferUsage.IndexBuffer | BufferUsage.Dynamic));
            }

            for (var i = 0; i < draw_data.CmdListsCount; i++) {
                var cmd_list = draw_data.CmdListsRange[i];

                cl.UpdateBuffer(
                    m_VertexBuffer,
                    vertex_offset_in_vertices * (uint)Unsafe.SizeOf<ImDrawVert>(),
                    cmd_list.VtxBuffer.Data,
                    (uint)(cmd_list.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>()));

                cl.UpdateBuffer(
                    m_IndexBuffer,
                    index_offset_in_elements * sizeof(ushort),
                    cmd_list.IdxBuffer.Data,
                    (uint)(cmd_list.IdxBuffer.Size * sizeof(ushort)));

                vertex_offset_in_vertices += (uint)cmd_list.VtxBuffer.Size;
                index_offset_in_elements += (uint)cmd_list.IdxBuffer.Size;
            }

            // Setup orthographic projection matrix into our constant buffer
            var io = ImGui.GetIO();
            var mvp = Matrix4x4.CreateOrthographicOffCenter(
                0f,
                io.DisplaySize.X,
                io.DisplaySize.Y,
                0.0f,
                -1.0f,
                1.0f);

            m_GraphicsDevice.UpdateBuffer(m_ProjMatrixBuffer, 0, ref mvp);

            cl.SetVertexBuffer(0, m_VertexBuffer);
            cl.SetIndexBuffer(m_IndexBuffer, IndexFormat.UInt16);
            cl.SetPipeline(m_Pipeline);
            cl.SetGraphicsResourceSet(0, m_MainResourceSet);

            draw_data.ScaleClipRects(io.DisplayFramebufferScale);

            // Render command lists
            var vtx_offset = 0;
            var idx_offset = 0;
            for (var n = 0; n < draw_data.CmdListsCount; n++) {
                var cmd_list = draw_data.CmdListsRange[n];
                for (var cmd_i = 0; cmd_i < cmd_list.CmdBuffer.Size; cmd_i++) {
                    var pcmd = cmd_list.CmdBuffer[cmd_i];
                    if (pcmd.UserCallback != IntPtr.Zero) {
                        throw new NotImplementedException();
                    }
                    else {
                        if (pcmd.TextureId != IntPtr.Zero) {
                            if (pcmd.TextureId == m_FontAtlasID) {
                                cl.SetGraphicsResourceSet(1, m_FontTextureResourceSet);
                            }
                            else {
                                cl.SetGraphicsResourceSet(1, GetImageResourceSet(pcmd.TextureId));
                            }
                        }

                        cl.SetScissorRect(
                            0,
                            (uint)pcmd.ClipRect.X,
                            (uint)pcmd.ClipRect.Y,
                            (uint)(pcmd.ClipRect.Z - pcmd.ClipRect.X),
                            (uint)(pcmd.ClipRect.W - pcmd.ClipRect.Y));

                        cl.DrawIndexed(pcmd.ElemCount, 1, (uint)idx_offset, vtx_offset, 0);
                    }

                    idx_offset += (int)pcmd.ElemCount;
                }
                vtx_offset += cmd_list.VtxBuffer.Size;
            }
        }

        /// <summary>
        /// Frees all graphics resources used by the renderer.
        /// </summary>
        public void Dispose()
        {
            m_VertexBuffer.Dispose();
            m_IndexBuffer.Dispose();
            m_ProjMatrixBuffer.Dispose();
            m_FontTexture.Dispose();
            m_FontTextureView.Dispose();
            m_VertexShader.Dispose();
            m_FragmentShader.Dispose();
            m_Layout.Dispose();
            m_TextureLayout.Dispose();
            m_Pipeline.Dispose();
            m_MainResourceSet.Dispose();

            foreach (var resource in m_OwnedResources) {
                resource.Dispose();
            }
        }

        private struct ResourceSetInfo
        {
            public readonly IntPtr ImGuiBinding;
            public readonly ResourceSet ResourceSet;

            public ResourceSetInfo(IntPtr imGuiBinding, ResourceSet resourceSet)
            {
                ImGuiBinding = imGuiBinding;
                ResourceSet = resourceSet;
            }
        }
    }
}
