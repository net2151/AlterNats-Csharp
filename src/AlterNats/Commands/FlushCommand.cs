﻿using AlterNats.Internal;

namespace AlterNats.Commands;

internal sealed class AsyncFlushCommand : AsyncCommandBase<AsyncFlushCommand>
{
    AsyncFlushCommand()
    {

    }

    public static AsyncFlushCommand Create(ObjectPool pool)
    {
        if (!TryRent(pool, out var result))
        {
            result = new AsyncFlushCommand();
        }

        return result;
    }

    public override void Write(ProtocolWriter writer)
    {
    }

    protected override void Reset()
    {
    }
}
