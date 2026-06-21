using System;
using System.Collections.Generic;

namespace Wavekeep.Core
{
    /// <summary>
    /// Instance-based, type-keyed event bus for decoupled gameplay signals
    /// (see CLAUDE.md §3.3). Owned by <see cref="GameSession"/> and torn down with it.
    ///
    /// Deliberately NOT a static singleton and NOT built on static C# events:
    /// static events risk ghost listeners surviving scene reloads (CLAUDE.md §3.5).
    ///
    /// Pattern choice (Task 01): a single <c>Dictionary&lt;Type, Delegate&gt;</c> mapping
    /// each event type to its combined multicast handler. Chosen over per-event explicit
    /// C# events so that adding a new event type requires zero edits to this class.
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

        /// <summary>Register <paramref name="handler"/> to receive events of type <typeparamref name="T"/>.</summary>
        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var existing))
            {
                _handlers[type] = (Action<T>)existing + handler;
            }
            else
            {
                _handlers[type] = handler;
            }
        }

        /// <summary>Remove a previously-registered <paramref name="handler"/> for type <typeparamref name="T"/>.</summary>
        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var existing)) return;

            var updated = (Action<T>)existing - handler;
            if (updated == null)
            {
                _handlers.Remove(type);
            }
            else
            {
                _handlers[type] = updated;
            }
        }

        /// <summary>Synchronously dispatch <paramref name="evt"/> to all handlers of type <typeparamref name="T"/>.</summary>
        public void Publish<T>(T evt)
        {
            if (_handlers.TryGetValue(typeof(T), out var existing))
            {
                ((Action<T>)existing)?.Invoke(evt);
            }
        }

        /// <summary>Drop every handler. Called on session teardown to prevent listeners leaking across runs/scenes.</summary>
        public void UnsubscribeAll()
        {
            _handlers.Clear();
        }
    }
}
