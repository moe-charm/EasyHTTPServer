using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace EasyHttpServer.Server;

public sealed record WebsitePreflightResult(bool IsValidRoot, bool HasIndex, string? Error = null);

public static class WebsitePreflight
{
    public static WebsitePreflightResult Inspect(string rootPath)
    {
        if (!WebsitePathPolicy.IsLocalWebsiteRoot(rootPath))
        {
            return new(false, false, "ローカルの通常フォルダーだけをWebサイトとして選択できます。");
        }

        try
        {
            using var lease = new PublishedRootLease(rootPath);
            foreach (var name in new[] { "index.html", "index.htm" })
            {
                try
                {
                    using var stream = lease.OpenFile(Path.Combine(lease.RootPath, name));
                    return new(true, true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return new(true, false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return new(false, false, exception.Message);
        }
    }
}

internal sealed class PublishedRootLease : IDisposable
{
    private readonly List<SafeFileHandle> _ancestorHandles = [];
    private bool _disposed;

    public PublishedRootLease(string rootPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Website publication currently requires Windows handle validation.");
        }

        RootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        try
        {
            _ancestorHandles.AddRange(OpenDirectoryChain(RootPath));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public string RootPath { get; }

    public FileStream OpenFile(string fullPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var expected = Path.GetFullPath(fullPath);
        if (!IsWithinRoot(expected))
        {
            throw new UnauthorizedAccessException("The requested file is outside the published root.");
        }

        var parent = Path.GetDirectoryName(expected)
            ?? throw new UnauthorizedAccessException("The requested file has no parent directory.");
        var requestDirectoryHandles = OpenDirectoryChain(parent, RootPath);
        try
        {
            var handle = OpenHandle(expected, isDirectory: false);
            try
            {
                ValidateHandle(handle, expected, requireDirectory: false);
                return new FileStream(handle, FileAccess.Read, bufferSize: 64 * 1024, isAsync: false);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }
        finally
        {
            foreach (var handle in requestDirectoryHandles)
            {
                handle.Dispose();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var handle in _ancestorHandles)
        {
            handle.Dispose();
        }

        _ancestorHandles.Clear();
    }

    private bool IsWithinRoot(string path) =>
        path.Equals(RootPath, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith($"{RootPath}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static List<SafeFileHandle> OpenDirectoryChain(string target, string? startAt = null)
    {
        var fullTarget = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target));
        var root = startAt is null
            ? Path.GetPathRoot(fullTarget) ?? throw new UnauthorizedAccessException("The path has no volume root.")
            : Path.TrimEndingDirectorySeparator(Path.GetFullPath(startAt));
        var relative = Path.GetRelativePath(root, fullTarget);
        if (Path.IsPathRooted(relative) || relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("The directory chain is outside the trusted root.");
        }

        var handles = new List<SafeFileHandle>();
        try
        {
            var current = root;
            if (startAt is null)
            {
                var rootHandle = OpenHandle(current, isDirectory: true);
                ValidateHandle(rootHandle, current, requireDirectory: true);
                handles.Add(rootHandle);
            }

            if (!relative.Equals(".", StringComparison.Ordinal))
            {
                foreach (var component in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
                {
                    current = Path.Combine(current, component);
                    var handle = OpenHandle(current, isDirectory: true);
                    ValidateHandle(handle, current, requireDirectory: true);
                    handles.Add(handle);
                }
            }

            return handles;
        }
        catch
        {
            foreach (var handle in handles)
            {
                handle.Dispose();
            }

            throw;
        }
    }

    private static SafeFileHandle OpenHandle(string path, bool isDirectory)
    {
        const uint genericRead = 0x80000000;
        const uint shareRead = 0x00000001;
        const uint shareWrite = 0x00000002;
        const uint openExisting = 3;
        const uint fileFlagOpenReparsePoint = 0x00200000;
        const uint fileFlagBackupSemantics = 0x02000000;
        const uint fileFlagSequentialScan = 0x08000000;

        var handle = CreateFileW(
            path,
            genericRead,
            shareRead | shareWrite,
            IntPtr.Zero,
            openExisting,
            fileFlagOpenReparsePoint | (isDirectory ? fileFlagBackupSemantics : fileFlagSequentialScan),
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new IOException($"Published path could not be opened safely: {new Win32Exception(error).Message}", error);
        }

        return handle;
    }

    private static void ValidateHandle(SafeFileHandle handle, string expectedPath, bool requireDirectory)
    {
        if (!GetFileInformationByHandleEx(
                handle,
                FileInfoByHandleClass.FileAttributeTagInfo,
                out var attributes,
                (uint)Marshal.SizeOf<FileAttributeTagInfo>()))
        {
            throw new IOException("Published path attributes could not be verified.", Marshal.GetLastWin32Error());
        }

        var isDirectory = (attributes.FileAttributes & (uint)FileAttributes.Directory) != 0;
        var isReparsePoint = (attributes.FileAttributes & (uint)FileAttributes.ReparsePoint) != 0;
        if (isReparsePoint || isDirectory != requireDirectory)
        {
            throw new UnauthorizedAccessException("Reparse points and unexpected path types are not publishable.");
        }

        var finalPath = GetFinalPath(handle);
        var expected = NormalizeFinalPath(Path.GetFullPath(expectedPath));
        if (!finalPath.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Published path identity changed during validation.");
        }
    }

    private static string GetFinalPath(SafeFileHandle handle)
    {
        var buffer = new char[32768];
        var length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, 0);
        if (length == 0 || length >= buffer.Length)
        {
            throw new IOException("Published path identity could not be read.", Marshal.GetLastWin32Error());
        }

        return NormalizeFinalPath(new string(buffer, 0, (int)length));
    }

    private static string NormalizeFinalPath(string path)
    {
        const string prefix = @"\\?\";
        var normalized = path.StartsWith(prefix, StringComparison.Ordinal) ? path[prefix.Length..] : path;
        return Path.TrimEndingDirectorySeparator(normalized);
    }

    private enum FileInfoByHandleClass
    {
        FileAttributeTagInfo = 9,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileAttributeTagInfo
    {
        public uint FileAttributes;
        public uint ReparseTag;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle file,
        FileInfoByHandleClass fileInformationClass,
        out FileAttributeTagInfo fileInformation,
        uint bufferSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(
        SafeFileHandle file,
        [Out] char[] filePath,
        uint filePathLength,
        uint flags);
}
