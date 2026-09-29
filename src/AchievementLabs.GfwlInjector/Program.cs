using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AchievementLabs.GfwlInjector;

internal static class Program
{
    private const uint ProcessCreateThread = 0x0002;
    private const uint ProcessQueryInformation = 0x0400;
    private const uint ProcessVmOperation = 0x0008;
    private const uint ProcessVmWrite = 0x0020;
    private const uint ProcessVmRead = 0x0010;
    private const uint MemCommit = 0x1000;
    private const uint MemReserve = 0x2000;
    private const uint MemRelease = 0x8000;
    private const uint PageReadWrite = 0x04;
    private const uint WaitObject0 = 0x00000000;
    private const uint WaitTimeout = 0x00000102;
    private const int TimeoutMs = 15000;

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length < 3 || !args[0].Equals("inject", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("Usage: AchievementLabs.GfwlInjector.exe inject <pid> <engine.dll>");
                return 2;
            }

            if (!int.TryParse(args[1], out var processId) || processId <= 0)
                throw new ArgumentException("Invalid process id.");

            var enginePath = Path.GetFullPath(args[2]);
            if (!File.Exists(enginePath))
                throw new FileNotFoundException("engine.dll was not found.", enginePath);

            using var target = Process.GetProcessById(processId);
            if (!Environment.Is64BitProcess && Is64BitProcess(target))
                throw new InvalidOperationException("The selected process is 64-bit. The imported GFWLAM engine is 32-bit and can only be injected into 32-bit GFWL games.");

            Inject(processId, enginePath);
            Console.WriteLine($"Injected engine.dll into PID {processId}.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void Inject(int processId, string dllPath)
    {
        var dllBytes = Encoding.Unicode.GetBytes(dllPath + "\0");
        var process = OpenProcess(
            ProcessCreateThread | ProcessQueryInformation | ProcessVmOperation | ProcessVmWrite | ProcessVmRead,
            false,
            processId);
        if (process == IntPtr.Zero)
            ThrowLastWin32("OpenProcess");

        IntPtr remotePath = IntPtr.Zero;
        IntPtr remoteThread = IntPtr.Zero;
        try
        {
            remotePath = VirtualAllocEx(process, IntPtr.Zero, (nuint)dllBytes.Length, MemCommit | MemReserve, PageReadWrite);
            if (remotePath == IntPtr.Zero)
                ThrowLastWin32("VirtualAllocEx");

            if (!WriteProcessMemory(process, remotePath, dllBytes, (nuint)dllBytes.Length, out var written) ||
                written != (nuint)dllBytes.Length)
            {
                ThrowLastWin32("WriteProcessMemory");
            }

            var kernel32 = GetModuleHandle("kernel32.dll");
            if (kernel32 == IntPtr.Zero)
                ThrowLastWin32("GetModuleHandle(kernel32.dll)");

            var loadLibrary = GetProcAddress(kernel32, "LoadLibraryW");
            if (loadLibrary == IntPtr.Zero)
                ThrowLastWin32("GetProcAddress(LoadLibraryW)");

            remoteThread = CreateRemoteThread(process, IntPtr.Zero, 0, loadLibrary, remotePath, 0, IntPtr.Zero);
            if (remoteThread == IntPtr.Zero)
                ThrowLastWin32("CreateRemoteThread");

            var wait = WaitForSingleObject(remoteThread, TimeoutMs);
            if (wait == WaitTimeout)
                throw new TimeoutException("Timed out waiting for remote LoadLibraryW.");
            if (wait != WaitObject0)
                ThrowLastWin32("WaitForSingleObject");

            if (!GetExitCodeThread(remoteThread, out var exitCode))
                ThrowLastWin32("GetExitCodeThread");
            if (exitCode == 0)
                throw new InvalidOperationException("Remote LoadLibraryW returned 0. engine.dll was not loaded.");
        }
        finally
        {
            if (remoteThread != IntPtr.Zero)
                CloseHandle(remoteThread);
            if (remotePath != IntPtr.Zero)
                VirtualFreeEx(process, remotePath, 0, MemRelease);
            CloseHandle(process);
        }
    }

    private static bool Is64BitProcess(Process process)
    {
        if (!Environment.Is64BitOperatingSystem)
            return false;

        if (!IsWow64Process(process.Handle, out var isWow64))
            ThrowLastWin32("IsWow64Process");

        return !isWow64;
    }

    private static void ThrowLastWin32(string operation)
    {
        throw new Win32Exception(Marshal.GetLastWin32Error(), $"{operation} failed");
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address, nuint size, uint allocationType, uint protect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFreeEx(IntPtr process, IntPtr address, nuint size, uint freeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr process, IntPtr baseAddress, byte[] buffer, nuint size, out nuint bytesWritten);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string moduleName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr module, string procName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateRemoteThread(IntPtr process, IntPtr threadAttributes, nuint stackSize, IntPtr startAddress, IntPtr parameter, uint creationFlags, IntPtr threadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, int milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeThread(IntPtr thread, out uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool IsWow64Process(IntPtr process, out bool wow64Process);
}
