﻿namespace AlterNats.Commands;

internal sealed class SubscribeCommand : CommandBase<SubscribeCommand>
{
    NatsKey? subject;
    int subscriptionId;

    SubscribeCommand()
    {
    }

    public static SubscribeCommand Create(int subscriptionId, string subject)
    {
        if (!pool.TryPop(out var result))
        {
            result = new SubscribeCommand();
        }

        result.subject = new NatsKey(subject); // TODO:use specified overload.
        result.subscriptionId = subscriptionId;

        return result;
    }

    public static SubscribeCommand Create(int subscriptionId, NatsKey subject)
    {
        if (!pool.TryPop(out var result))
        {
            result = new SubscribeCommand();
        }

        result.subject = subject;
        result.subscriptionId = subscriptionId;

        return result;
    }

    public override string WriteTraceMessage => "Write SUB Command to buffer.";

    public override void Write(ProtocolWriter writer)
    {
        writer.WriteSubscribe(subscriptionId, subject!);
    }

    public override void Return()
    {
        subject = null;
        base.Return();
    }
}
