namespace EasyHttpServer.Desktop.Wpf;

internal static class CommandExceptionPolicy
{
    public static bool IsRecoverable(Exception exception) => exception is not (
        OutOfMemoryException or
        AccessViolationException or
        AppDomainUnloadedException or
        BadImageFormatException or
        CannotUnloadAppDomainException or
        InvalidProgramException or
        StackOverflowException);
}
