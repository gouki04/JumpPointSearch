using ImGuiNET;
using System.Numerics;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace jps
{
    class Program
    {
        private static Sdl2Window ms_Window;
        private static GraphicsDevice ms_GraphisDevice;
        private static CommandList ms_CmdList;
        private static ImGuiController ms_Controller;
        private static JPSEditor ms_Editor;

        // UI state
        private static Vector3 ms_ClearColor = new Vector3(0.45f, 0.55f, 0.6f);

        static void Main(string[] args)
        {
            // Create window, GraphicsDevice, and all resources necessary for the demo.
            VeldridStartup.CreateWindowAndGraphicsDevice(
                new WindowCreateInfo(50, 50, 1280, 720, WindowState.Normal, "Armchair Detective"),
                new GraphicsDeviceOptions(true, null, true),
                out ms_Window,
                out ms_GraphisDevice);
            ms_Window.Resized += () =>
            {
                ms_GraphisDevice.MainSwapchain.Resize((uint)ms_Window.Width, (uint)ms_Window.Height);
                ms_Controller.WindowResized(ms_Window.Width, ms_Window.Height);
            };

            ms_CmdList = ms_GraphisDevice.ResourceFactory.CreateCommandList();
            ms_Controller = new ImGuiController(ms_GraphisDevice, ms_GraphisDevice.MainSwapchain.Framebuffer.OutputDescription, ms_Window.Width, ms_Window.Height);
            ms_Editor = new JPSEditor();
            ms_Editor.Init();

            // Main application loop
            while (ms_Window.Exists) {
                var snapshot = ms_Window.PumpEvents();
                if (!ms_Window.Exists) { break; }
                ms_Controller.Update(1f / 60f, snapshot); // Feed the input events to our ImGui controller, which passes them through to ImGui.

                var quit = ms_Editor.DrawGui();

                ms_CmdList.Begin();
                ms_CmdList.SetFramebuffer(ms_GraphisDevice.MainSwapchain.Framebuffer);
                ms_CmdList.ClearColorTarget(0, new RgbaFloat(ms_ClearColor.X, ms_ClearColor.Y, ms_ClearColor.Z, 1f));
                ms_Controller.Render(ms_GraphisDevice, ms_CmdList);
                ms_CmdList.End();
                ms_GraphisDevice.SubmitCommands(ms_CmdList);
                ms_GraphisDevice.SwapBuffers(ms_GraphisDevice.MainSwapchain);

                if (quit) {
                    break;
                }
            }

            // Clean up Veldrid resources
            ms_GraphisDevice.WaitForIdle();
            ms_Controller.Dispose();
            ms_CmdList.Dispose();
            ms_GraphisDevice.Dispose();
        }
    }
}
