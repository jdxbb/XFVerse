using System.Runtime.InteropServices;

namespace MediaLibrary.Core.Helpers;

public static class NativeRuntimeResolver
{
    public const ushort PeMachineX86 = 0x014c;
    public const ushort PeMachineX64 = 0x8664;
    public const ushort PeMachineArm64 = 0xAA64;

    public static string? GetCurrentRuntimeId()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "win-x64",
            Architecture.Arm64 => "win-arm64",
            _ => null
        };
    }

    public static ushort? GetExpectedPeMachine()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => PeMachineX64,
            Architecture.Arm64 => PeMachineArm64,
            Architecture.X86 => PeMachineX86,
            _ => null
        };
    }

    public static PeMachineInfo TryReadPeMachine(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            if (stream.Length < 64)
            {
                return PeMachineInfo.Invalid("too-small");
            }

            var mz = reader.ReadUInt16();
            if (mz != 0x5A4D)
            {
                return PeMachineInfo.NonPe();
            }

            stream.Seek(0x3C, SeekOrigin.Begin);
            var peHeaderOffset = reader.ReadUInt32();
            if (peHeaderOffset == 0 || peHeaderOffset + 6 > stream.Length)
            {
                return PeMachineInfo.Invalid("invalid-pe-offset");
            }

            stream.Seek(peHeaderOffset, SeekOrigin.Begin);
            var signature = reader.ReadUInt32();
            if (signature != 0x00004550)
            {
                return PeMachineInfo.Invalid("invalid-pe-signature");
            }

            return PeMachineInfo.Valid(reader.ReadUInt16());
        }
        catch
        {
            return PeMachineInfo.Invalid("read-failed");
        }
    }

    public static string FormatPeMachine(ushort machine)
    {
        return machine switch
        {
            PeMachineX64 => "x64",
            PeMachineX86 => "x86",
            PeMachineArm64 => "arm64",
            _ => $"unknown-0x{machine:X4}"
        };
    }
}

public sealed record PeMachineInfo(bool IsPe, bool IsValid, ushort? Machine, string Architecture, string? Error)
{
    public static PeMachineInfo Valid(ushort machine)
    {
        return new PeMachineInfo(true, true, machine, NativeRuntimeResolver.FormatPeMachine(machine), null);
    }

    public static PeMachineInfo NonPe()
    {
        return new PeMachineInfo(false, false, null, "non-pe", null);
    }

    public static PeMachineInfo Invalid(string error)
    {
        return new PeMachineInfo(false, false, null, "invalid-pe", error);
    }
}
