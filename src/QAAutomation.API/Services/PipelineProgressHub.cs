using System.Collections.Concurrent;
using System.Threading.Channels;
using QAAutomation.API.DTOs;

namespace QAAutomation.API.Services;

/// <summary>
/// In-process pub/sub hub for pipeline progress events.
///
/// When <see cref="CallPipelineService"/> finishes each item it calls
/// <see cref="Publish"/> with a <see cref="PipelineProgressEventDto"/>.
/// The SSE endpoint in <c>CallPipelineController</c> subscribes via
/// <see cref="Subscribe"/> and streams the events to the browser.
///
/// Registered as a singleton so the same instance is shared between
/// the service layer and the controller layer.
/// </summary>
public sealed class PipelineProgressHub
{
    // JobId → list of subscriber channels
    private readonly ConcurrentDictionary<int, List<Channel<PipelineProgressEventDto>>> _subs = new();

    /// <summary>Opens a channel that will receive progress events for <paramref name="jobId"/>.</summary>
    public Channel<PipelineProgressEventDto> Subscribe(int jobId)
    {
        var ch = Channel.CreateUnbounded<PipelineProgressEventDto>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        _subs.AddOrUpdate(jobId,
            _ => [ch],
            (_, list) => { lock (list) { list.Add(ch); } return list; });

        return ch;
    }

    /// <summary>Removes and completes a subscriber channel.</summary>
    public void Unsubscribe(int jobId, Channel<PipelineProgressEventDto> ch)
    {
        if (_subs.TryGetValue(jobId, out var list))
        {
            lock (list) { list.Remove(ch); }
        }
        ch.Writer.TryComplete();
    }

    /// <summary>Publishes an event to all current subscribers for the given job.</summary>
    public void Publish(int jobId, PipelineProgressEventDto evt)
    {
        if (!_subs.TryGetValue(jobId, out var list)) return;
        List<Channel<PipelineProgressEventDto>> snapshot;
        lock (list) { snapshot = [.. list]; }
        foreach (var ch in snapshot)
            ch.Writer.TryWrite(evt);
    }

    /// <summary>
    /// Completes all subscriber channels for a job (called when the job finishes).
    /// </summary>
    public void Complete(int jobId)
    {
        if (_subs.TryRemove(jobId, out var list))
        {
            lock (list)
                foreach (var ch in list)
                    ch.Writer.TryComplete();
        }
    }
}
