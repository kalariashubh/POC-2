using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

class Program
{
    static string acadExe =
        @"C:\Program Files\Autodesk\AutoCAD 2022\acad.exe";

    static string dwgPath =
    @"D:\Buniyad Byte\POC 2\svf-dwg-dbId-boundary\data\input.dwg";

    static string scriptPath =
        @"D:\Buniyad Byte\POC 2\svf-dwg-dbId-boundary\scripts\run.scr";

    static string triggerPath =
        @"D:\Buniyad Byte\POC 2\svf-dwg-dbId-boundary\server\storage\run_autocad.flag";

    static void Main()
    {
        Console.WriteLine("🟢 AutoCAD Automation Agent started");

        while (true)
        {
            if (File.Exists(triggerPath))
            {
                Console.WriteLine("📌 Trigger detected");

                RunAutoCAD();

                File.Delete(triggerPath);
                Console.WriteLine("✅ Trigger cleared");
            }

            Thread.Sleep(2000);
        }
    }

    static void RunAutoCAD()
    {
        // 🔴 STEP 3: disable startup LISP safely
        Environment.SetEnvironmentVariable(
            "ACADLSPASDOC",
            "0",
            EnvironmentVariableTarget.Process
        );

        Process.Start(new ProcessStartInfo
        {
            FileName = acadExe,

            // 🔴 STEP 1 + 2: absolute script path + correct flags
            Arguments = $"/nologo \"{dwgPath}\" /b \"{scriptPath}\"",

            UseShellExecute = false
        });
    }
}
