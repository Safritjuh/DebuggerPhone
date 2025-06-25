using System;
using WindowsSipPhone.Tests;

namespace WindowsSipPhone
{
    class ProfileTestRunner
    {
        static void Main(string[] args)
        {
            Console.WriteLine("IMP-016 Profile Handler Test Suite");
            Console.WriteLine("===================================");
            Console.WriteLine();
            
            try
            {
                ProfileHandlerTest.RunTests();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running tests: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
