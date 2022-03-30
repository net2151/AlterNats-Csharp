﻿using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace AlterNats;

internal sealed class SocketClosedException : Exception
{
    public SocketClosedException(Exception? innerException)
        : base("Socket has been closed.", innerException)
    {

    }
}

internal sealed class PhysicalConnection : IAsyncDisposable
{
    readonly Socket socket;
    readonly TaskCompletionSource<Exception> waitForClosedSource = new();
    int disposed;

    public Task<Exception> WaitForClosed => waitForClosedSource.Task;

    public PhysicalConnection()
    {
        this.socket = new Socket(Socket.OSSupportsIPv6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        if (Socket.OSSupportsIPv6)
        {
            socket.DualMode = true;
        }

        socket.NoDelay = true;
        socket.SendBufferSize = 0;
        socket.ReceiveBufferSize = 0;
    }

    // CancellationToken is not used, operation lifetime is completely same as socket.

    // socket is closed:
    //  receiving task returns 0 read
    //  throws SocketException when call method
    // socket is disposed:
    //  throws DisposedException

    // return ValueTask directly for performance, not care exception and signal-disconected.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask ConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        return socket.ConnectAsync(host, port, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<int> SendAsync(ReadOnlyMemory<byte> buffer, SocketFlags socketFlags)
    {
        return socket.SendAsync(buffer, socketFlags, CancellationToken.None);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<int> ReceiveAsync(Memory<byte> buffer, SocketFlags socketFlags)
    {
        return socket.ReceiveAsync(buffer, socketFlags, CancellationToken.None);
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Increment(ref disposed) == 1)
        {
            try
            {
                waitForClosedSource.TrySetCanceled();
            }
            catch { }
            socket.Dispose();
        }
        return default;
    }

    // when catch SocketClosedException, call this method.
    public void SignalDisconnected(Exception exception)
    {
        waitForClosedSource.TrySetResult(exception);
    }
}
