using System.Runtime.InteropServices;

namespace NtBot.Connector.Windows.Providers.Profit;

/// <summary>
/// Sessão DDEML única por thread STA — Profit usa serviço profitchart, tópico COT, item ATIVO.ULT.
/// </summary>
internal sealed class ProfitDdeSession : NativeWindow, IDisposable
{
    private const int CfText = 1;
    private const int XtypAdvstart = 0x1030;
    private const int XtypRequest = 0x20B0;
    private const int XtypAdvdData = 0x4010;
    private const int DdeFack = 0x8000;
    private const int DdeFnotProcessed = 0x0000;
    private const int AppclassStandard = 0x00000001;
    private const int AppcmdClientonly = 0x00000010;
    private const int CpWin1252 = 1252;
    private const int CpWinAnsi = 0;

    private readonly DdeCallbackDelegate _callbackDelegate;
    private readonly Dictionary<string, Action<string, string>> _itemHandlers = new(StringComparer.OrdinalIgnoreCase);

    private uint _instanceId;
    private IntPtr _conversation;
    private bool _initialized;
    private string _server = string.Empty;
    private string _topic = string.Empty;

    public ProfitDdeSession()
    {
        _callbackDelegate = OnDdeCallback;
        CreateHandle(new CreateParams());
    }

    public string CurrentTopic => _topic;

    public bool IsConnected => _conversation != IntPtr.Zero;

    public void Connect(string server, string topic)
    {
        if (_initialized && _conversation != IntPtr.Zero
            && server.Equals(_server, StringComparison.OrdinalIgnoreCase)
            && topic.Equals(_topic, StringComparison.OrdinalIgnoreCase))
            return;

        DisconnectConversation();

        if (!_initialized)
        {
            var result = DdeInitialize(
                out _instanceId,
                _callbackDelegate,
                AppclassStandard | AppcmdClientonly,
                0);
            if (result != 0)
                throw new InvalidOperationException($"DdeInitialize falhou: {DescribeError(result)}");
            _initialized = true;
        }

        _server = server;
        _topic = topic;

        var serverHandle = CreateStringHandle(server);
        var topicHandle = CreateStringHandle(topic);

        if (serverHandle == IntPtr.Zero || topicHandle == IntPtr.Zero)
            throw new InvalidOperationException(
                $"DdeCreateStringHandle falhou para {server}|{topic} ({DescribeLastError()})");

        try
        {
            _conversation = DdeConnect(_instanceId, serverHandle, topicHandle, IntPtr.Zero);
            if (_conversation == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"DdeConnect falhou para {server}|{topic} ({DescribeLastError()})");
        }
        finally
        {
            if (serverHandle != IntPtr.Zero) DdeFreeStringHandle(_instanceId, serverHandle);
            if (topicHandle != IntPtr.Zero) DdeFreeStringHandle(_instanceId, topicHandle);
        }
    }

    public void StartAdvise(string item, Action<string, string> handler)
    {
        if (!TryStartAdvise(item, handler, out var error))
            throw new InvalidOperationException(error ?? $"StartAdvise falhou para {item}");
    }

    public bool TryStartAdvise(string item, Action<string, string> handler, out string? error)
    {
        error = null;
        if (_conversation == IntPtr.Zero)
        {
            error = "DDE não conectado";
            return false;
        }

        var itemHandle = CreateStringHandle(item);
        if (itemHandle == IntPtr.Zero)
        {
            error = $"Item DDE inválido: {item} ({DescribeLastError()})";
            return false;
        }

        try
        {
            if (TryAdviseTransaction(itemHandle, IntPtr.Zero, 0))
            {
                _itemHandlers[item] = handler;
                return true;
            }

            var zero = GCHandle.Alloc(0, GCHandleType.Pinned);
            try
            {
                if (TryAdviseTransaction(itemHandle, zero.AddrOfPinnedObject(), sizeof(int)))
                {
                    _itemHandlers[item] = handler;
                    return true;
                }
            }
            finally
            {
                zero.Free();
            }

            var interval = GCHandle.Alloc(1000, GCHandleType.Pinned);
            try
            {
                if (TryAdviseTransaction(itemHandle, interval.AddrOfPinnedObject(), sizeof(int)))
                {
                    _itemHandlers[item] = handler;
                    return true;
                }
            }
            finally
            {
                interval.Free();
            }

            error = $"StartAdvise falhou para {item} ({DescribeLastError()})";
            return false;
        }
        finally
        {
            DdeFreeStringHandle(_instanceId, itemHandle);
        }
    }

    private bool TryAdviseTransaction(IntPtr itemHandle, IntPtr data, int size)
    {
        var result = DdeClientTransaction(
            data, size, _conversation, itemHandle, CfText, XtypAdvstart, 10000, IntPtr.Zero);
        return result != IntPtr.Zero;
    }

    public string? Request(string item)
    {
        if (_conversation == IntPtr.Zero)
            return null;

        return RequestInternal(item, XtypRequest);
    }

    private string? RequestInternal(string item, int transactionType)
    {
        var itemHandle = CreateStringHandle(item);
        if (itemHandle == IntPtr.Zero)
            return null;

        try
        {
            var result = DdeClientTransaction(
                IntPtr.Zero, 0, _conversation, itemHandle, CfText, transactionType, 5000, IntPtr.Zero);
            if (result == IntPtr.Zero)
                return null;

            try
            {
                return AccessData(result);
            }
            finally
            {
                DdeFreeDataHandle(result);
            }
        }
        finally
        {
            if (itemHandle != IntPtr.Zero) DdeFreeStringHandle(_instanceId, itemHandle);
        }
    }

    private IntPtr OnDdeCallback(uint type, uint fmt, IntPtr hconv, IntPtr hsz1, IntPtr hsz2, IntPtr hdata, IntPtr dw1, IntPtr dw2)
    {
        if (type == XtypAdvdData && hdata != IntPtr.Zero)
        {
            var item = QueryString(hsz2);
            var text = AccessData(hdata);
            DispatchAdvise(item, text);
            DdeFreeDataHandle(hdata);
            return (IntPtr)DdeFack;
        }

        return (IntPtr)DdeFnotProcessed;
    }

    private void DispatchAdvise(string item, string text)
    {
        if (_itemHandlers.TryGetValue(item, out var handler))
        {
            handler(item, text);
            return;
        }

        foreach (var (registeredItem, registeredHandler) in _itemHandlers)
        {
            if (ItemMatches(registeredItem, item))
            {
                registeredHandler(item, text);
                return;
            }
        }
    }

    private static bool ItemMatches(string registered, string incoming)
    {
        if (registered.Equals(incoming, StringComparison.OrdinalIgnoreCase))
            return true;

        var normalizedRegistered = registered.Trim('[', ']');
        var normalizedIncoming = incoming.Trim('[', ']');
        return normalizedRegistered.Equals(normalizedIncoming, StringComparison.OrdinalIgnoreCase);
    }

    private string QueryString(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return string.Empty;

        var len = DdeQueryString(_instanceId, handle, null!, 0, CpWin1252);
        if (len <= 0)
            return string.Empty;

        var buffer = new char[len];
        DdeQueryString(_instanceId, handle, buffer, len, CpWin1252);
        return new string(buffer).TrimEnd('\0');
    }

    private static string AccessData(IntPtr hdata)
    {
        var ptr = DdeAccessData(hdata, out _);
        if (ptr == IntPtr.Zero)
            return string.Empty;

        try
        {
            return Marshal.PtrToStringAnsi(ptr) ?? string.Empty;
        }
        finally
        {
            DdeUnaccessData(hdata);
        }
    }

    private IntPtr CreateStringHandle(string value)
    {
        foreach (var codePage in new[] { CpWin1252, CpWinAnsi })
        {
            var handle = DdeCreateStringHandle(_instanceId, value, codePage);
            if (handle != IntPtr.Zero)
                return handle;
        }

        return IntPtr.Zero;
    }

    private string DescribeLastError() => DescribeError(DdeGetLastError(_instanceId));

    private static string DescribeError(uint code) => code switch
    {
        0x0000 => "DMLERR_NO_ERROR",
        0x4000 => "DMLERR_ADVACKTIMEOUT",
        0x4001 => "DMLERR_BUSY",
        0x4002 => "DMLERR_DATAACKTIMEOUT",
        0x4003 => "DMLERR_DLL_NOT_INITIALIZED",
        0x4004 => "DMLERR_DLL_USAGE",
        0x4005 => "DMLERR_EXECACKTIMEOUT",
        0x4006 => "DMLERR_INVALIDPARAMETER",
        0x4007 => "DMLERR_LOW_MEMORY",
        0x4008 => "DMLERR_MEMORY_ERROR",
        0x4009 => "DMLERR_NOTPROCESSED",
        0x400A => "DMLERR_NO_CONV_ESTABLISHED",
        0x400B => "DMLERR_POKEACKTIMEOUT",
        0x400C => "DMLERR_POSTMSG_FAILED",
        0x400D => "DMLERR_REENTRANCY",
        0x400E => "DMLERR_SERVER_DIED",
        0x400F => "DMLERR_SYS_ERROR",
        0x4010 => "DMLERR_UNADVACKTIMEOUT",
        0x4011 => "DMLERR_UNFOUND_QUEUE_ID",
        _ => $"0x{code:X4}"
    };

    private void DisconnectConversation()
    {
        if (_conversation != IntPtr.Zero)
        {
            DdeDisconnect(_conversation);
            _conversation = IntPtr.Zero;
        }

        _itemHandlers.Clear();
    }

    public void Dispose()
    {
        DisconnectConversation();

        if (_initialized)
        {
            DdeUninitialize(_instanceId);
            _initialized = false;
            _instanceId = 0;
        }

        if (Handle != IntPtr.Zero)
            DestroyHandle();
    }

    private delegate IntPtr DdeCallbackDelegate(uint uType, uint uFmt, IntPtr hconv, IntPtr hsz1, IntPtr hsz2, IntPtr hdata, IntPtr dwData1, IntPtr dwData2);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern uint DdeInitialize(out uint pidInst, DdeCallbackDelegate pfnCallback, uint afCmd, uint ulRes);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern IntPtr DdeConnect(uint idInst, IntPtr hszService, IntPtr hszTopic, IntPtr pCC);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern bool DdeDisconnect(IntPtr hconv);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern IntPtr DdeCreateStringHandle(uint idInst, string psz, int iCodePage);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern bool DdeFreeStringHandle(uint idInst, IntPtr hsz);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern IntPtr DdeClientTransaction(IntPtr pData, int cbData, IntPtr hconv, IntPtr hszItem, int wFmt, int wType, int dwTimeout, IntPtr pdwResult);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern int DdeQueryString(uint idInst, IntPtr hsz, char[]? psz, int cchMax, int iCodePage);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern IntPtr DdeAccessData(IntPtr hData, out int pcbDataSize);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern bool DdeUnaccessData(IntPtr hData);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern bool DdeFreeDataHandle(IntPtr hData);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern uint DdeGetLastError(uint idInst);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern bool DdeUninitialize(uint idInst);
}
