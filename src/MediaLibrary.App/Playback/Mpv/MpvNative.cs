using System.Runtime.InteropServices;

namespace MediaLibrary.App.Playback.Mpv;

public static class MpvNative
{
    private const string LibraryName = "libmpv-2";

    public static MpvNativeLoadResult EnsureLoaded()
    {
        return MpvNativeLoader.TryLoad();
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_create")]
    public static extern IntPtr Create();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_initialize")]
    public static extern int Initialize(IntPtr handle);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_destroy")]
    public static extern void Destroy(IntPtr handle);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_terminate_destroy")]
    public static extern void TerminateDestroy(IntPtr handle);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_set_option_string")]
    public static extern int SetOptionString(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_set_property")]
    public static extern int SetProperty(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        MpvFormat format,
        IntPtr data);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_set_property_string")]
    public static extern int SetPropertyString(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_get_property")]
    public static extern int GetProperty(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        MpvFormat format,
        IntPtr data);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_get_property_string")]
    public static extern IntPtr GetPropertyString(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_free")]
    public static extern void Free(IntPtr data);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_free_node_contents")]
    public static extern void FreeNodeContents(IntPtr node);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_command")]
    public static extern int Command(IntPtr handle, IntPtr args);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_command_async")]
    public static extern int CommandAsync(IntPtr handle, ulong replyUserData, IntPtr args);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_observe_property")]
    public static extern int ObserveProperty(
        IntPtr handle,
        ulong replyUserData,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        MpvFormat format);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_wait_event")]
    public static extern IntPtr WaitEvent(IntPtr handle, double timeout);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_request_log_messages")]
    public static extern int RequestLogMessages(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string minLevel);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_error_string")]
    public static extern IntPtr ErrorString(int error);
}
