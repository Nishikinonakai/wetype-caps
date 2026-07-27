using System.Runtime.InteropServices;

namespace WeTypeCaps;

internal static class SelfTests
{
    internal static int Run()
    {
        var failures = new List<string>();

        Check(
            Marshal.SizeOf<TsfProfileDetector.TF_INPUTPROCESSORPROFILE>() ==
                (nint.Size == 8 ? 88 : 72),
            "TF_INPUTPROCESSORPROFILE layout",
            failures);
        Check(
            Marshal.SizeOf<NativeMethods.INPUT>() == (nint.Size == 8 ? 40 : 28),
            "INPUT layout",
            failures);

        var monitor = new NativeMethods.RECT(0, 0, 1920, 1080);
        Check(
            FullscreenDetector.CoversMonitor(
                new NativeMethods.RECT(0, 0, 1920, 1080),
                monitor,
                2),
            "exact fullscreen",
            failures);
        Check(
            !FullscreenDetector.CoversMonitor(
                new NativeMethods.RECT(0, 0, 1920, 1040),
                monitor,
                2),
            "maximized work-area is not fullscreen",
            failures);
        Check(
            FullscreenDetector.CoversMonitor(
                new NativeMethods.RECT(-1, -1, 1921, 1081),
                monitor,
                2),
            "border tolerance",
            failures);

        var identity = new TsfProfileIdentity(Guid.NewGuid(), Guid.NewGuid(), "test");
        var active = new ActiveProfile(
            0,
            1,
            0x0804,
            identity.Clsid,
            identity.ProfileGuid,
            Guid.NewGuid(),
            3);
        Check(active.IsMatch(identity), "TSF exact identity match", failures);
        Check(!active.IsMatch(identity with { ProfileGuid = Guid.NewGuid() }), "TSF mismatch", failures);

        if (failures.Count == 0)
        {
            Console.WriteLine("Self-test passed (7 checks).");
            return 0;
        }

        foreach (string failure in failures)
        {
            Console.Error.WriteLine("FAILED: " + failure);
        }

        return 1;
    }

    private static void Check(bool condition, string name, ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add(name);
        }
    }
}
