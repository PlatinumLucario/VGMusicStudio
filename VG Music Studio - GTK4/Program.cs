using Adw;
using System;
using System.Runtime.InteropServices;

namespace Kermalis.VGMusicStudio.GTK4
{
	internal class Program
	{
		private static readonly Application _app = Application.New("org.Kermalis.VGMusicStudio.GTK4", Gio.ApplicationFlags.FlagsNone);
		private static readonly OSPlatform Linux = OSPlatform.Linux;
		private static readonly OSPlatform FreeBSD = OSPlatform.FreeBSD;

		static void OnActivate(Gio.Application sender, EventArgs e)
		{

		}

		[STAThread]
		public static void Main(string[] args)
		{
			_app.Register(Gio.Cancellable.GetCurrent());

			if (!RuntimeInformation.IsOSPlatform(Linux) | !RuntimeInformation.IsOSPlatform(FreeBSD))
			{
				if (GLib.Functions.Getenv("GDK_BACKEND") is not "wayland")
				{
					GLib.Functions.Setenv("GSK_RENDERER", "cairo", false);
				}
			}

			_app.OnActivate += OnActivate;

			var argv = new string[args.Length + 1];
			argv[0] = "Kermalis.VGMusicStudio.GTK4";
			args.CopyTo(argv, 1);

			// Set an initial?
			string initial = "";
			if (args.Length > 0)
				initial = args[0].Trim();

			// Add Main Window
			var win = new MainWindow(_app);
			_app.AddWindow(win);
			win.Present();
			_app.Run(args.Length, args);
		}
	}
}
