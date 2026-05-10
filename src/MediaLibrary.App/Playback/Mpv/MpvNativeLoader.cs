using System.IO;
using System.Runtime.InteropServices;
using MediaLibrary.App.Helpers;

namespace MediaLibrary.App.Playback.Mpv;

public static class MpvNativeLoader
{
    private const string LibraryFileName = "libmpv-2.dll";
    private const uint LoadLibrarySearchDefaultDirs = 0x00001000;
    private const uint LoadLibrarySearchUserDirs = 0x00000400;
    private static readonly object SyncRoot = new();
    private static IntPtr _libraryHandle;
    private static MpvNativeLoadResult? _lastResult;

    public static MpvNativeLoadResult TryLoad()
    {
        lock (SyncRoot)
        {
            if (_lastResult?.Succeeded == true && _libraryHandle != IntPtr.Zero)
            {
                return _lastResult;
            }

            if (!TryResolveNativeDirectory(out var selectedRid, out var nativeDirectory, out var unsupportedResult))
            {
                _lastResult = unsupportedResult;
                return _lastResult;
            }

            var libraryPath = Path.GetFullPath(Path.Combine(nativeDirectory, LibraryFileName));
            MpvPlaybackDiagnostics.Write("mpv-native-load-start library=libmpv-2");
            WriteEnvironmentDiagnostics(selectedRid, nativeDirectory, libraryPath);

            if (!File.Exists(libraryPath))
            {
                _lastResult = new MpvNativeLoadResult
                {
                    Succeeded = false,
                    NativeDirectory = nativeDirectory,
                    LibraryPath = libraryPath,
                    Error = "\u64ad\u653e\u5668\u5185\u6838\u6587\u4ef6\u7f3a\u5931\uff0c\u8bf7\u5b89\u88c5\u5f53\u524d\u7cfb\u7edf\u67b6\u6784\u5bf9\u5e94\u7684 mpv \u5185\u6838\u6587\u4ef6\u3002",
                    ErrorType = "MissingNativeLibrary"
                };
                MpvPlaybackDiagnostics.Write($"mpv-native-missing selectedRid={selectedRid} file=libmpv-2.dll");
                MpvPlaybackDiagnostics.Write("mpv-native-load-result success=false errorType=MissingNativeLibrary");
                return _lastResult;
            }

            WritePeDiagnostics(libraryPath);
            WriteMpvDirectoryFileList(nativeDirectory);

            try
            {
                ConfigureWindowsDllSearchPath(nativeDirectory);

                MpvPlaybackDiagnostics.Write($"mpv-native-load-begin libmpvPath={libraryPath}");
                _libraryHandle = NativeLibrary.Load(libraryPath);
                _lastResult = new MpvNativeLoadResult
                {
                    Succeeded = true,
                    NativeDirectory = nativeDirectory,
                    LibraryPath = libraryPath
                };
                MpvPlaybackDiagnostics.Write("p/invoke layer initialized library=libmpv-2");
                MpvPlaybackDiagnostics.Write("mpv-native-load-result success=true");
                return _lastResult;
            }
            catch (BadImageFormatException exception)
            {
                _lastResult = CreateFailedResult(
                    nativeDirectory,
                    libraryPath,
                    "\u64ad\u653e\u5668\u5185\u6838\u67b6\u6784\u4e0d\u5339\u914d\uff0c\u8bf7\u786e\u8ba4\u4f7f\u7528\u5f53\u524d\u7cfb\u7edf\u67b6\u6784\u5bf9\u5e94\u7684 libmpv-2.dll\u3002",
                    exception);
                return _lastResult;
            }
            catch (DllNotFoundException exception)
            {
                _lastResult = CreateFailedResult(
                    nativeDirectory,
                    libraryPath,
                    "\u64ad\u653e\u5668\u5185\u6838\u4f9d\u8d56\u6587\u4ef6\u7f3a\u5931\uff0c\u8bf7\u786e\u8ba4 mpv \u76ee\u5f55\u5b8c\u6574\u3002",
                    exception);
                return _lastResult;
            }
            catch (Exception exception)
            {
                _lastResult = CreateFailedResult(
                    nativeDirectory,
                    libraryPath,
                    "\u64ad\u653e\u5668\u5185\u6838\u52a0\u8f7d\u5931\u8d25\uff0c\u8bf7\u786e\u8ba4 mpv \u6587\u4ef6\u5b8c\u6574\u3002",
                    exception);
                return _lastResult;
            }
        }
    }

    public static string ResolveNativeDirectory()
    {
        return TryResolveNativeDirectory(out _, out var nativeDirectory, out _)
            ? nativeDirectory
            : Path.Combine(AppContext.BaseDirectory, "mpv");
    }

    private static bool TryResolveNativeDirectory(
        out string selectedRid,
        out string nativeDirectory,
        out MpvNativeLoadResult unsupportedResult)
    {
        var processArchitecture = RuntimeInformation.ProcessArchitecture;
        selectedRid = processArchitecture switch
        {
            Architecture.X64 => "win-x64",
            Architecture.Arm64 => "win-arm64",
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(selectedRid))
        {
            nativeDirectory = Path.Combine(AppContext.BaseDirectory, "mpv", selectedRid);
            unsupportedResult = null!;
            return true;
        }

        nativeDirectory = Path.Combine(AppContext.BaseDirectory, "mpv");
        var libraryPath = Path.Combine(nativeDirectory, LibraryFileName);
        unsupportedResult = new MpvNativeLoadResult
        {
            Succeeded = false,
            NativeDirectory = nativeDirectory,
            LibraryPath = libraryPath,
            Error = "\u5f53\u524d\u7cfb\u7edf\u67b6\u6784\u6682\u4e0d\u652f\u6301\uff0c\u8bf7\u4f7f\u7528 Windows x64 \u6216 Windows ARM64 \u7248\u672c\u3002",
            ErrorType = "UnsupportedProcessArchitecture"
        };
        MpvPlaybackDiagnostics.Write("mpv-native-load-start library=libmpv-2");
        MpvPlaybackDiagnostics.Write(
            $"mpv-native-architecture-unsupported processArchitecture={processArchitecture} osArchitecture={RuntimeInformation.OSArchitecture} selectedRid=unsupported");
        MpvPlaybackDiagnostics.Write("mpv-native-load-result success=false errorType=UnsupportedProcessArchitecture");
        return false;
    }

    private static MpvNativeLoadResult CreateFailedResult(
        string nativeDirectory,
        string libraryPath,
        string error,
        Exception exception)
    {
        var errorType = exception.GetType().Name;
        MpvPlaybackDiagnostics.Write(
            $"mpv-native-load-failed errorType={errorType} hresult=0x{exception.HResult:X8} message={NormalizeLogValue(exception.Message)}");
        MpvPlaybackDiagnostics.Write($"mpv-native-load-result success=false errorType={errorType}");
        return new MpvNativeLoadResult
        {
            Succeeded = false,
            NativeDirectory = nativeDirectory,
            LibraryPath = libraryPath,
            Error = error,
            ErrorType = errorType
        };
    }

    private static void WriteEnvironmentDiagnostics(string selectedRid, string nativeDirectory, string libraryPath)
    {
        try
        {
            MpvPlaybackDiagnostics.Write($"mpv-native-app-base-directory value={AppContext.BaseDirectory}");
            MpvPlaybackDiagnostics.Write($"mpv-native-current-directory value={Directory.GetCurrentDirectory()}");
            MpvPlaybackDiagnostics.Write($"mpv-native-selected selectedRid={selectedRid}");
            MpvPlaybackDiagnostics.Write($"mpv-native-selected-mpv-dir selectedMpvDir={nativeDirectory}");
            MpvPlaybackDiagnostics.Write($"mpv-native-selected-libmpv-path selectedLibmpvPath={libraryPath}");
            MpvPlaybackDiagnostics.Write(
                $"mpv-native-path-check selectedRid={selectedRid} directoryExists={Directory.Exists(nativeDirectory).ToString().ToLowerInvariant()} fileExists={File.Exists(libraryPath).ToString().ToLowerInvariant()}");
            MpvPlaybackDiagnostics.Write(
                $"mpv-native-architecture is64BitProcess={Environment.Is64BitProcess.ToString().ToLowerInvariant()} osArchitecture={RuntimeInformation.OSArchitecture} processArchitecture={RuntimeInformation.ProcessArchitecture} selectedRid={selectedRid}");
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write($"mpv-native-diagnostics-failed errorType={exception.GetType().Name}");
        }
    }

    private static void WriteMpvDirectoryFileList(string nativeDirectory)
    {
        try
        {
            if (!Directory.Exists(nativeDirectory))
            {
                MpvPlaybackDiagnostics.Write("mpv-native-directory-files skipped=missing-directory");
                return;
            }

            foreach (var filePath in Directory.EnumerateFiles(nativeDirectory, "*", SearchOption.TopDirectoryOnly)
                         .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                var fileInfo = new FileInfo(filePath);
                MpvPlaybackDiagnostics.Write($"mpv-native-directory-file name={fileInfo.Name} size={fileInfo.Length}");
            }
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write($"mpv-native-directory-files-failed errorType={exception.GetType().Name}");
        }
    }

    private static void WritePeDiagnostics(string libraryPath)
    {
        try
        {
            using var stream = File.Open(libraryPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new BinaryReader(stream);
            if (stream.Length < 0x40)
            {
                MpvPlaybackDiagnostics.Write($"mpv-native-pe valid=false reason=too-small length={stream.Length}");
                return;
            }

            var mz = reader.ReadUInt16();
            if (mz != 0x5A4D)
            {
                MpvPlaybackDiagnostics.Write($"mpv-native-pe valid=false reason=invalid-mz mz=0x{mz:X4}");
                return;
            }

            stream.Seek(0x3C, SeekOrigin.Begin);
            var peHeaderOffset = reader.ReadInt32();
            if (peHeaderOffset <= 0 || peHeaderOffset > stream.Length - 6)
            {
                MpvPlaybackDiagnostics.Write(
                    $"mpv-native-pe valid=false reason=invalid-pe-offset peHeaderOffset={peHeaderOffset} length={stream.Length}");
                return;
            }

            stream.Seek(peHeaderOffset, SeekOrigin.Begin);
            var signature = reader.ReadUInt32();
            if (signature != 0x00004550)
            {
                MpvPlaybackDiagnostics.Write(
                    $"mpv-native-pe valid=false reason=invalid-pe-signature peHeaderOffset={peHeaderOffset} signature=0x{signature:X8}");
                return;
            }

            var machine = reader.ReadUInt16();
            MpvPlaybackDiagnostics.Write(
                $"mpv-native-pe valid=true peHeaderOffset={peHeaderOffset} signature=0x{signature:X8} machine=0x{machine:X4} architecture={FormatPeMachine(machine)}");
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write($"mpv-native-pe failed errorType={exception.GetType().Name}");
        }
    }

    private static string FormatPeMachine(ushort machine)
    {
        return machine switch
        {
            0x8664 => "x64",
            0x014c => "x86",
            0xAA64 => "arm64",
            _ => "unknown"
        };
    }

    private static void ConfigureWindowsDllSearchPath(string nativeDirectory)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var setDefaultResult = SetDefaultDllDirectories(LoadLibrarySearchDefaultDirs | LoadLibrarySearchUserDirs);
            MpvPlaybackDiagnostics.Write(
                $"mpv-native-set-default-dll-directories result={setDefaultResult.ToString().ToLowerInvariant()} lastError={Marshal.GetLastWin32Error()}");
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write($"mpv-native-set-default-dll-directories-failed errorType={exception.GetType().Name}");
        }

        try
        {
            var addDirectoryCookie = AddDllDirectory(nativeDirectory);
            MpvPlaybackDiagnostics.Write(
                $"mpv-native-add-dll-directory result={(addDirectoryCookie != IntPtr.Zero).ToString().ToLowerInvariant()} lastError={Marshal.GetLastWin32Error()}");
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write($"mpv-native-add-dll-directory-failed errorType={exception.GetType().Name}");
        }

        try
        {
            var setDirectoryResult = SetDllDirectory(nativeDirectory);
            MpvPlaybackDiagnostics.Write(
                $"mpv-native-set-dll-directory result={setDirectoryResult.ToString().ToLowerInvariant()} lastError={Marshal.GetLastWin32Error()}");
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write($"mpv-native-set-dll-directory-failed errorType={exception.GetType().Name}");
        }
    }

    private static string NormalizeLogValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "empty"
            : value.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetDefaultDllDirectories(uint directoryFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr AddDllDirectory(string newDirectory);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);
}
