namespace MyServiceBus;

public static class ErrorTransportSettlement
{
    private const string ErrorAddressDataKey = "MyServiceBus.ErrorTransportAddress";

    public static void MarkMoved(Exception exception, Uri errorAddress)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(errorAddress);
        exception.Data[ErrorAddressDataKey] = errorAddress;
    }

    public static bool WasMoved(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.Data.Contains(ErrorAddressDataKey);
    }

    public static Uri? GetErrorAddress(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.Data[ErrorAddressDataKey] as Uri;
    }
}
