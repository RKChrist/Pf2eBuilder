using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Pf2e.Api.Hosting;

/// <summary>
/// Ties a child process to this one, so it cannot outlive it.
/// <para>Stopping cleanly is not the case this exists for. A stop button, a Ctrl+C or a crash all
/// end this process without the chance to tidy up, and a client left holding port 5173 afterwards
/// is worse than no client at all: the next run finds the port taken, leaves it alone, and serves
/// whatever that older build put there. That is the stale-asset trap this repository already has
/// a warning about, arriving by a new route.</para>
/// <para>Windows gives a job object with kill-on-close for exactly this. Anywhere else this does
/// nothing and the ordinary stop path is what tidies up.</para>
/// </summary>
static class ChildLifetime
{
    const uint KillOnJobClose = 0x2000;
    const int ExtendedLimitInformation = 9;

    static nint _job;

    /// <summary>True when the child is now tied to this process's own lifetime.</summary>
    public static bool Bind(Process child)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            if (_job == 0)
            {
                _job = CreateJobObject(0, null);
                if (_job == 0 || !KillChildrenWithUs(_job))
                {
                    return false;
                }
            }

            return AssignProcessToJobObject(_job, child.Handle);
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    static bool KillChildrenWithUs(nint job)
    {
        var limits = new ExtendedLimit
        {
            Basic = new BasicLimit { LimitFlags = KillOnJobClose },
        };

        var size = Marshal.SizeOf<ExtendedLimit>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(limits, buffer, fDeleteOld: false);
            return SetInformationJobObject(job, ExtendedLimitInformation, buffer, (uint)size);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct BasicLimit
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ExtendedLimit
    {
        public BasicLimit Basic;
        public IoCounters Io;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern nint CreateJobObject(nint attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetInformationJobObject(nint job, int infoClass, nint info, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool AssignProcessToJobObject(nint job, nint process);
}
