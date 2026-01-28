using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Lotus;

/// <summary>
/// Custom entry point for unpackaged WinUI 3 application
/// </summary>
public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        try 
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();

            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
        catch (Exception ex)
        {
             var logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "crash_log.txt");
             System.IO.File.WriteAllText(logPath, "PROGRAM CRASH:\n" + ex.ToString());
        }
    }
}
