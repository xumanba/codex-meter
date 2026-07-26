using System;
using System.Threading;

internal static class HangingCli
{
    private static int Main()
    {
        Thread.Sleep(TimeSpan.FromMinutes(1));
        return 0;
    }
}
