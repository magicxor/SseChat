namespace SseChat;

// Manages messages for a single chat room in a thread-safe manner,
// using a SortedList keyed by GUID v7 for ordered storage.
public class ChatRoomLog
{
    // Stores messages sorted by their GUID v7 ID, which is time-ordered.
    private readonly SortedList<Guid, ChatMessage> _messages = new();
    private readonly ReaderWriterLockSlim _lock = new();

    // Adds a new message to the log.
    // The message's ID (GUID v7) is used as the key, ensuring chronological order.
    public void AddMessage(ChatMessage message)
    {
        _lock.EnterWriteLock();
        try
        {
            // The Guid v7 ID itself ensures messages are sorted chronologically.
            _messages.Add(message.Id, message);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    // Retrieves messages from a specific point in time (inclusive).
    public List<ChatMessage> GetMessagesSince(DateTime startTime)
    {
        _lock.EnterReadLock();
        try
        {
            var result = new List<ChatMessage>();

            // _messages.Values is an IList<ChatMessage> sorted by the Guid keys.
            // Since Guid v7 is time-ordered, this collection is also time-ordered.
            // We iterate through the values and filter by the Timestamp.
            foreach (ChatMessage msg in _messages.Values)
            {
                if (msg.Timestamp >= startTime)
                {
                    result.Add(msg);
                }
            }
            return result;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    // Retrieves all messages in the log, in chronological order.
    public List<ChatMessage> GetAllMessages()
    {
        _lock.EnterReadLock();
        try
        {
            // _messages.Values returns an IList<ChatMessage> which is a view of the values in sorted order.
            // ToList() creates a new list, making it safe to return.
            return _messages.Values.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    // Trims messages older than a specified cutoff date or keeps only a certain number of recent messages.
    public void TrimMessages(DateTime? cutoffDate = null, int? maxMessagesToKeep = null)
    {
        _lock.EnterWriteLock();
        try
        {
            bool needsRebuild = false;
            List<Guid> keysToRemove = new List<Guid>();

            if (cutoffDate.HasValue)
            {
                // Iterate backwards to efficiently remove or collect keys for removal
                // (though SortedList removal by key isn't by index, collecting keys first is safer during iteration)
                foreach (var kvp in _messages) // Iterates in sorted order (oldest first)
                {
                    if (kvp.Value.Timestamp < cutoffDate.Value)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                    else
                    {
                        // Since messages are sorted by time, once we find a message
                        // at or after the cutoff, subsequent messages will also be.
                        break;
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _messages.Remove(key);
                }
                needsRebuild = keysToRemove.Any(); // Not strictly needed if just removing
                keysToRemove.Clear(); // Prepare for next potential trim condition
            }

            if (maxMessagesToKeep.HasValue && _messages.Count > maxMessagesToKeep.Value)
            {
                int numToRemove = _messages.Count - maxMessagesToKeep.Value;
                // Keys are Guids, values are ChatMessages. IList<TKey> gives access to keys by index.
                IList<Guid> keys = _messages.Keys;
                for (int i = 0; i < numToRemove; i++)
                {
                    // Add oldest keys for removal
                    keysToRemove.Add(keys[i]);
                }

                foreach (var key in keysToRemove)
                {
                    _messages.Remove(key);
                }
                // No rebuild needed, SortedList handles its structure.
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}
