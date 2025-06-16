using System;
using System.Windows;

namespace WindowsSipPhone
{
    class TestIncomingCall
    {
        public static void TestIncomingCallWindow()
        {
            Console.WriteLine("[TEST] Creating test incoming call window");
            
            // Test with a typical SIP caller info format
            string testCallerInfo = "\"John Doe\" <sip:101@192.168.1.180>";
            
            var testWindow = new IncomingCallWindow(testCallerInfo);
            testWindow.ShowDialog();
        }
    }
}
