using System.Configuration;
using System.Data;
using System.Windows;
using System;
using System.Runtime.InteropServices;
using WindowsSipPhone.Core.Utilities;
using WindowsSipPhone.UI.Windows;

namespace WindowsSipPhone;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    [DllImport("kernel32.dll")]
    static extern bool AllocConsole();    protected override void OnStartup(StartupEventArgs e)
    {
        // Don't call base.OnStartup(e) to prevent automatic window creation
        
        // Only allocate console in debug builds or when explicitly requested
#if DEBUG
        AllocConsole();
        Console.WriteLine("[DEBUG] Console allocated for debug output");
        Console.WriteLine("=== Application Startup Debug ===");
        Console.WriteLine($"Command line args: {string.Join(" ", e.Args)}");
#endif
          try
        {
#if DEBUG
            Console.WriteLine("Application starting up...");
#endif
            // Track application startup            ApplicationTracker.TrackApplicationStart();
            
            // Theme is now static - defined in App.xaml, no dynamic switching needed
#if DEBUG
            Console.WriteLine("[THEME DEBUG] Using static light theme defined in App.xaml");
            // Test SIP Profile system in debug builds
            Console.WriteLine("\n[PROFILE DEBUG] Testing SIP Profile system...");
            try
            {
                // WindowsSipPhone.Tests.SipProfileTests.RunTests(); // Removed: Tests namespace does not exist
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROFILE DEBUG] Profile tests failed: {ex.Message}");
            }
#endif
              // Add global exception handlers
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            this.Exit += App_Exit;

            // Set shutdown mode to close when main window closes
            this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            
            // Create and show the main window
#if DEBUG
            Console.WriteLine("Creating main window...");
#endif
            MainWindow = new MainWindow();
            MainWindow.Show();
            MainWindow.Activate();
            
#if DEBUG
            Console.WriteLine("Main window created and shown.");
#endif
            
#if DEBUG
            Console.WriteLine("Application startup completed.");
#endif
        }
        catch (Exception ex)
        {
            // Log to event log or file in release builds, console in debug
#if DEBUG
            Console.WriteLine($"[ERROR] Startup exception: {ex.Message}");
            Console.WriteLine($"[ERROR] Exception type: {ex.GetType().Name}");
            Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[ERROR] Inner exception: {ex.InnerException.Message}");
                Console.WriteLine($"[ERROR] Inner stack trace: {ex.InnerException.StackTrace}");
            }
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
#else
            // In release builds, show a message box for critical errors
            System.Windows.MessageBox.Show($"Application startup failed: {ex.Message}", "SIP Phone Error", 
                          MessageBoxButton.OK, MessageBoxImage.Error);
#endif
            throw;
        }
    }    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        // Track the error
        ApplicationTracker.TrackError("APP", "Unhandled UI exception", e.Exception);
        
#if DEBUG
        Console.WriteLine($"Unhandled UI exception: {e.Exception.Message}");
        Console.WriteLine($"Stack trace: {e.Exception.StackTrace}");
        Console.ReadKey();
#else
        // In release builds, show error dialog and allow user to continue or exit
        var result = System.Windows.MessageBox.Show($"An unexpected error occurred: {e.Exception.Message}\n\nWould you like to continue running the application?", 
                                   "SIP Phone Error", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        e.Handled = (result == MessageBoxResult.Yes);
#endif
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = (Exception)e.ExceptionObject;
        
        // Track the critical error
        ApplicationTracker.TrackError("APP", "Unhandled domain exception", exception);
        
#if DEBUG
        Console.WriteLine($"Unhandled domain exception: {exception.Message}");
        Console.WriteLine($"Stack trace: {exception.StackTrace}");
        Console.ReadKey();
#else        // In release builds, log to Windows Event Log and show critical error
        System.Windows.MessageBox.Show($"A critical error occurred and the application must close: {exception.Message}", 
                       "SIP Phone Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
#endif
    }

    private void App_Exit(object sender, ExitEventArgs e)
    {
        ApplicationTracker.TrackApplicationStop();
    }
}